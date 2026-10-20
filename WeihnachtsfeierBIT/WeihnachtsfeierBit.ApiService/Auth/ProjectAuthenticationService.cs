using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class ProjectAuthenticationService(
    WeihnachtsfeierDbContext dbContext,
    IConfiguration configuration)
{
    private static readonly TimeSpan AdminSessionLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ParticipantSessionLifetime = TimeSpan.FromDays(1);
    private static readonly PasswordHasher PasswordHasher = new();

    public async Task<(SessionResponse? Session, ProjektAdmin? Admin, string? Error)> RegisterAdminAccountAsync(
        CreateAdminAccountRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var cleanUsername = request.Username.Trim();
        var normalizedUsername = cleanUsername.ToUpperInvariant();

        if (await dbContext.ProjektAdmins.AnyAsync(admin => admin.BenutzernameNormalisiert == normalizedUsername, cancellationToken))
        {
            return (null, null, "Der Benutzername ist bereits vergeben.");
        }

        var admin = new ProjektAdmin
        {
            Benutzername = cleanUsername,
            BenutzernameNormalisiert = normalizedUsername,
            PasswortHash = PasswordHasher.Hash(request.Password),
            ErstelltAmUtc = DateTime.UtcNow
        };

        dbContext.ProjektAdmins.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (session, error) = await CreateAdminSessionAsync(admin, userAgent, cancellationToken);
        return (session, admin, error);
    }

    public async Task<(ProjektAdmin? Admin, string? Error)> AttachAdminToProjectAsync(
        Guid projectId,
        AttachAdminToProjectRequest request,
        CancellationToken cancellationToken)
    {
        var cleanUsername = request.Username.Trim();
        var normalizedUsername = cleanUsername.ToUpperInvariant();

        var admin = await dbContext.ProjektAdmins.FirstOrDefaultAsync(
            item => item.BenutzernameNormalisiert == normalizedUsername,
            cancellationToken);

        if (admin is null)
        {
            return (null, "Es wurde kein Admin-Konto mit diesem Benutzernamen gefunden.");
        }

        var exists = await dbContext.ProjektAdminProjekte.AnyAsync(
            membership => membership.ProjektAdminId == admin.Id && membership.ProjektId == projectId,
            cancellationToken);

        if (exists)
        {
            return (null, "Dieses Admin-Konto ist dem Projekt bereits zugeordnet.");
        }

        dbContext.ProjektAdminProjekte.Add(new ProjektAdminProjekt
        {
            ProjektAdminId = admin.Id,
            ProjektId = projectId,
            HinzugefuegtAmUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (admin, null);
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginParticipantAsync(
        Guid projectId,
        int number,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var activeInstance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        var participant = await dbContext.Teilnehmer.FirstOrDefaultAsync(
            item => item.ProjektId == projectId && item.Nummer == number,
            cancellationToken);
        if (participant is null)
        {
            return (null, "Teilnehmernummer oder Projekt ist ungültig.");
        }

        var rawToken = ApiToken.Create();
        var now = DateTime.UtcNow;
        var session = new Sitzung
        {
            TeilnehmerId = participant.Id,
            GameInstanceId = activeInstance?.Id,
            TokenHash = ApiToken.Hash(rawToken),
            ErstelltAmUtc = now,
            LetzteAktivitaetAmUtc = now,
            LaeuftAbAmUtc = now.Add(ParticipantSessionLifetime),
            UserAgent = NormalizeUserAgent(userAgent)
        };
        dbContext.Sitzungen.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new SessionResponse(session.Id, rawToken, session.LaeuftAbAmUtc, "participant", projectId,
            participant.Name ?? $"Teilnehmer {participant.Nummer}"), null);
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginParticipantByCodeAsync(
        string code,
        int number,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var cleanCode = code.Trim();
        var activeInstance = await dbContext.GameInstances
            .Include(gi => gi.Projekt)
            .FirstOrDefaultAsync(gi => gi.AccessCode == cleanCode, cancellationToken);

        if (activeInstance is null)
        {
            return (null, "Ungültiger oder abgelaufener Session-Code.");
        }

        var projectId = activeInstance.ProjektId;
        var participant = await dbContext.Teilnehmer.FirstOrDefaultAsync(
            item => item.ProjektId == projectId && item.Nummer == number,
            cancellationToken);

        if (participant is null)
        {
            participant = new Teilnehmer
            {
                ProjektId = projectId,
                Projekt = activeInstance.Projekt,
                Nummer = number,
                Name = null
            };
            dbContext.Teilnehmer.Add(participant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var rawToken = ApiToken.Create();
        var now = DateTime.UtcNow;
        var session = new Sitzung
        {
            TeilnehmerId = participant.Id,
            GameInstanceId = activeInstance.Id,
            TokenHash = ApiToken.Hash(rawToken),
            ErstelltAmUtc = now,
            LetzteAktivitaetAmUtc = now,
            LaeuftAbAmUtc = now.Add(ParticipantSessionLifetime),
            UserAgent = NormalizeUserAgent(userAgent)
        };
        dbContext.Sitzungen.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new SessionResponse(session.Id, rawToken, session.LaeuftAbAmUtc, "participant", projectId,
            participant.Name ?? $"Teilnehmer {participant.Nummer}"), null);
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginAdminAsync(
        AdminLoginRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var cleanUsername = request.Username.Trim();
        var normalizedUsername = cleanUsername.ToUpperInvariant();

        var admin = await dbContext.ProjektAdmins.FirstOrDefaultAsync(
            item => item.BenutzernameNormalisiert == normalizedUsername,
            cancellationToken);

        if (admin is null || !PasswordHasher.Verify(request.Password, admin.PasswortHash))
        {
            return (null, "Benutzername oder Passwort ist falsch.");
        }

        return await CreateAdminSessionAsync(admin, userAgent, cancellationToken);
    }

    public async Task RevokeAdminSessionAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = ApiToken.Hash(rawToken);
        var session = await dbContext.ProjektAdminSitzungen.FirstOrDefaultAsync(
            item => item.TokenHash == hash,
            cancellationToken);

        if (session is null || session.WiderrufenAmUtc.HasValue)
        {
            return;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeParticipantSessionAsync(Guid projectId, string rawToken, CancellationToken cancellationToken)
    {
        var hash = ApiToken.Hash(rawToken);
        var session = await dbContext.Sitzungen.FirstOrDefaultAsync(
            item => item.TokenHash == hash && item.Teilnehmer.ProjektId == projectId,
            cancellationToken);

        if (session is null || session.WiderrufenAmUtc.HasValue)
        {
            return;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(SessionResponse Session, string? Error)> CreateAdminSessionAsync(
        ProjektAdmin admin,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var rawToken = ApiToken.Create();
        var now = DateTime.UtcNow;
        var adminSession = new ProjektAdminSitzung
        {
            ProjektAdminId = admin.Id,
            TokenHash = ApiToken.Hash(rawToken),
            ErstelltAmUtc = now,
            LetzteAktivitaetAmUtc = now,
            LaeuftAbAmUtc = now.Add(AdminSessionLifetime),
            UserAgent = NormalizeUserAgent(userAgent)
        };

        dbContext.ProjektAdminSitzungen.Add(adminSession);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new SessionResponse(adminSession.Id, rawToken, adminSession.LaeuftAbAmUtc, "admin", null, admin.Benutzername), null);
    }

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var trimmed = userAgent.Trim();
        return trimmed.Length > 512 ? trimmed[..512] : trimmed;
    }
}
