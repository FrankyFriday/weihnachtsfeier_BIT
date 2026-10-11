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

    public async Task<(ProjectResponse? Project, SessionResponse? Session, string? Error)> CreateProjectAsync(
        CreateProjectRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var projectName = request.Name?.Trim() ?? string.Empty;
        var username = request.AdminUsername?.Trim() ?? string.Empty;
        var validationError = ValidateProjectAndAdmin(projectName, username, request.AdminPassword);
        if (validationError is not null)
        {
            return (null, null, validationError);
        }

        var project = new Projekt { Name = projectName };
        var admin = CreateAdmin(project, username, request.AdminPassword);
        var (adminSession, rawToken) = CreateAdminSession(admin, userAgent);
        dbContext.Projekte.Add(project);
        dbContext.ProjektAdmins.Add(admin);
        dbContext.ProjektAdminSitzungen.Add(adminSession);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new ProjectResponse(project.Id, project.Name),
            new SessionResponse(adminSession.Id, rawToken, adminSession.LaeuftAbAmUtc, "admin"), null);
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginParticipantAsync(
        Guid projectId,
        int number,
        string? userAgent,
        CancellationToken cancellationToken)
    {
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
            TokenHash = ApiToken.Hash(rawToken),
            ErstelltAmUtc = now,
            LetzteAktivitaetAmUtc = now,
            LaeuftAbAmUtc = now.Add(ParticipantSessionLifetime),
            UserAgent = NormalizeUserAgent(userAgent)
        };
        dbContext.Sitzungen.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new SessionResponse(session.Id, rawToken, session.LaeuftAbAmUtc, "participant"), null);
    }

    public async Task<(SessionResponse? Session, string? Error)> LoginAdminAsync(
        Guid projectId,
        AdminLoginRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(request.Username ?? string.Empty);
        var admin = await dbContext.ProjektAdmins.FirstOrDefaultAsync(
            item => item.ProjektId == projectId && item.BenutzernameNormalisiert == normalizedUsername,
            cancellationToken);
        if (admin is null || string.IsNullOrEmpty(request.Password) || !passwordHasher.Verify(request.Password, admin.PasswortHash))
        {
            return (null, "Benutzername, Passwort oder Projekt ist ungültig.");
        }

        var created = CreateAdminSession(admin, userAgent);
        dbContext.ProjektAdminSitzungen.Add(created.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (new SessionResponse(created.Entity.Id, created.RawToken, created.Entity.LaeuftAbAmUtc, "admin"), null);
    }

    public async Task<(ProjektAdmin? Admin, string? Error)> AddAdminAsync(
        Guid projectId,
        CreateAdminRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var validationError = ValidateAdmin(username, request.Password);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        if (!await dbContext.Projekte.AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return (null, "Projekt wurde nicht gefunden.");
        }

        var admin = new ProjektAdmin
        {
            ProjektId = projectId,
            Benutzername = username,
            BenutzernameNormalisiert = NormalizeUsername(username),
            PasswortHash = passwordHasher.Hash(request.Password!)
        };
        dbContext.ProjektAdmins.Add(admin);
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

    private ProjektAdmin CreateAdmin(Projekt project, string username, string password)
    {
        return new ProjektAdmin
        {
            Projekt = project,
            Benutzername = username,
            BenutzernameNormalisiert = NormalizeUsername(username),
            PasswortHash = passwordHasher.Hash(password)
        };
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

    private string? ValidateProjectAndAdmin(string projectName, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > 120)
        {
            return "Der Projektname muss zwischen 1 und 120 Zeichen lang sein.";
        }

        return ValidateAdmin(username, password);
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