using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class ProjectAuthenticationService(
    WeihnachtsfeierDbContext dbContext,
    PasswordHasher passwordHasher)
{
    private static readonly TimeSpan ParticipantSessionLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan AdminSessionLifetime = TimeSpan.FromHours(8);

    public async Task<(ProjektAdmin? Admin, SessionResponse? Session, string? Error)> RegisterAdminAccountAsync(
        CreateAdminAccountRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var validationError = ValidateAdmin(username, request.Password);
        if (validationError is not null)
        {
            return (null, null, validationError);
        }

        var normalizedUsername = NormalizeUsername(username);
        if (await dbContext.ProjektAdmins.AnyAsync(admin => admin.BenutzernameNormalisiert == normalizedUsername, cancellationToken))
        {
            return (null, null, "Dieser Admin-Benutzername ist bereits vergeben.");
        }

        var admin = new ProjektAdmin
        {
            Benutzername = username,
            BenutzernameNormalisiert = normalizedUsername,
            PasswortHash = passwordHasher.Hash(request.Password)
        };
        var (adminSession, rawToken) = CreateAdminSession(admin, userAgent);
        dbContext.ProjektAdmins.Add(admin);
        dbContext.ProjektAdminSitzungen.Add(adminSession);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (admin,
                new SessionResponse(adminSession.Id, rawToken, adminSession.LaeuftAbAmUtc, "admin", null, admin.Benutzername), null);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(admin).State = EntityState.Detached;
            dbContext.Entry(adminSession).State = EntityState.Detached;
            return (null, null, "Dieser Admin-Benutzername ist bereits vergeben.");
        }
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginParticipantAsync(
        Guid projectId,
        int number,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var activeInstance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId && gi.IsActive, cancellationToken);

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
            .FirstOrDefaultAsync(gi => gi.IsActive && gi.AccessCode == cleanCode, cancellationToken);

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
            // Auto-create participant if number doesn't exist in project yet
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
        var normalizedUsername = NormalizeUsername(request.Username ?? string.Empty);
        var admin = await dbContext.ProjektAdmins.FirstOrDefaultAsync(
            item => item.BenutzernameNormalisiert == normalizedUsername,
            cancellationToken);
        if (admin is null || string.IsNullOrEmpty(request.Password) || !passwordHasher.Verify(request.Password, admin.PasswortHash))
        {
            return (null, "Benutzername, Passwort oder Projekt ist ungültig.");
        }

        var created = CreateAdminSession(admin, userAgent);
        dbContext.ProjektAdminSitzungen.Add(created.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (new SessionResponse(created.Entity.Id, created.RawToken, created.Entity.LaeuftAbAmUtc, "admin", null, admin.Benutzername), null);
    }

    public async Task<(ProjektAdmin? Admin, string? Error)> AttachAdminToProjectAsync(
        Guid projectId,
        AttachAdminToProjectRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var admin = await dbContext.ProjektAdmins.FirstOrDefaultAsync(
            item => item.BenutzernameNormalisiert == NormalizeUsername(username), cancellationToken);
        if (admin is null)
        {
            return (null, "Für diesen Benutzernamen existiert noch kein Admin-Konto.");
        }

        if (!await dbContext.Projekte.AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return (null, "Projekt wurde nicht gefunden.");
        }

        if (await dbContext.ProjektAdminProjekte.AnyAsync(
            membership => membership.ProjektAdminId == admin.Id && membership.ProjektId == projectId,
            cancellationToken))
        {
            return (null, "Dieses Admin-Konto ist bereits dem Projekt zugeordnet.");
        }

        dbContext.ProjektAdminProjekte.Add(new ProjektAdminProjekt
        {
            ProjektAdminId = admin.Id,
            ProjektId = projectId
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (admin, null);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(admin).State = EntityState.Detached;
            return (null, "Ein Admin mit diesem Benutzernamen existiert bereits.");
        }
    }

    private (ProjektAdminSitzung Entity, string RawToken) CreateAdminSession(ProjektAdmin admin, string? userAgent)
    {
        var rawToken = ApiToken.Create();
        var now = DateTime.UtcNow;
        return (new ProjektAdminSitzung
        {
            ProjektAdmin = admin,
            TokenHash = ApiToken.Hash(rawToken),
            ErstelltAmUtc = now,
            LetzteAktivitaetAmUtc = now,
            LaeuftAbAmUtc = now.Add(AdminSessionLifetime),
            UserAgent = NormalizeUserAgent(userAgent)
        }, rawToken);
    }

    private static string NormalizeUsername(string? username) => username?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? NormalizeUserAgent(string? userAgent)
    {
        var normalized = userAgent?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized[..Math.Min(normalized.Length, 512)];
    }

    private static string? ValidateAdmin(string? username, string? password)
    {
        var normalizedUsername = username?.Trim();
        if (string.IsNullOrEmpty(normalizedUsername) || normalizedUsername.Length > 64)
        {
            return "Der Admin-Benutzername muss zwischen 1 und 64 Zeichen lang sein.";
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 256)
        {
            return "Das Admin-Passwort muss zwischen 12 und 256 Zeichen lang sein.";
        }

        return null;
    }
}