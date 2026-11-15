using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class ProjectAccessService(WeihnachtsfeierDbContext dbContext, ILogger<ProjectAccessService>? logger = null)
{
    private static readonly TimeSpan ActivityUpdateInterval = TimeSpan.FromSeconds(30);

    public async Task<ProjectAccess?> AuthenticateAsync(Guid projectId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            logger?.LogWarning("AuthenticateAsync fehlgeschlagen für Projekt {ProjectId}: Kein Token übergeben.", projectId);
            return null;
        }

        var now = DateTime.UtcNow;
        var tokenHash = ApiToken.Hash(token);
        var adminSession = await dbContext.ProjektAdminSitzungen
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash &&
                session.WiderrufenAmUtc == null &&
                session.LaeuftAbAmUtc > now, cancellationToken);

        if (adminSession is not null)
        {
            var project = await dbContext.Projekte
                .Include(p => p.Admins)
                .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

            if (project is not null)
            {
                bool isCreator = project.ErstellerAdminId == null || project.ErstellerAdminId == adminSession.ProjektAdminId;
                var membership = project.Admins.FirstOrDefault(a => a.ProjektAdminId == adminSession.ProjektAdminId);

                bool canControl = isCreator || (membership?.CanControlSession ?? true);
                bool canParticipants = isCreator || (membership?.CanManageParticipants ?? true);
                bool canSettings = isCreator || (membership?.CanEditProjectSettings ?? true);
                bool canShows = isCreator || (membership?.CanManageShows ?? true);

                if (now - adminSession.LetzteAktivitaetAmUtc >= ActivityUpdateInterval)
                {
                    adminSession.LetzteAktivitaetAmUtc = now;
                    try
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                    }
                }

                return new ProjectAccess(
                    projectId,
                    adminSession.Id,
                    adminSession.LaeuftAbAmUtc,
                    null,
                    adminSession.ProjektAdminId,
                    IsCreator: isCreator,
                    CanControlSession: canControl,
                    CanManageParticipants: canParticipants,
                    CanEditProjectSettings: canSettings,
                    CanManageShows: canShows);
            }

            logger?.LogWarning("AuthenticateAsync Admin-Sitzung gefunden für Admin {AdminId}, aber Projekt {ProjectId} existiert nicht in DB.", adminSession.ProjektAdminId, projectId);
        }

        var participantSession = await dbContext.Sitzungen
            .Include(session => session.Teilnehmer)
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash &&
                session.Teilnehmer.ProjektId == projectId &&
                session.WiderrufenAmUtc == null &&
                session.LaeuftAbAmUtc > now, cancellationToken);

        if (participantSession is null)
        {
            logger?.LogWarning("AuthenticateAsync fehlgeschlagen für Projekt {ProjectId}: Keine gültige Sitzung für TokenHash {HashPrefix}... gefunden.", projectId, tokenHash[..Math.Min(8, tokenHash.Length)]);
            return null;
        }

        if (now - participantSession.LetzteAktivitaetAmUtc >= ActivityUpdateInterval)
        {
            participantSession.LetzteAktivitaetAmUtc = now;
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
            }
        }

        return new ProjectAccess(projectId, participantSession.Id, participantSession.LaeuftAbAmUtc, participantSession.TeilnehmerId, null);
    }

    public async Task<(Guid SessionId, DateTime ExpiresAtUtc)?> RevokeParticipantSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = ApiToken.Hash(token);
        var session = await dbContext.Sitzungen.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.WiderrufenAmUtc == null, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (session.Id, session.LaeuftAbAmUtc);
    }

    public async Task<(Guid SessionId, DateTime ExpiresAtUtc)?> RevokeAdminSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = ApiToken.Hash(token);
        var session = await dbContext.ProjektAdminSitzungen.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.WiderrufenAmUtc == null, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (session.Id, session.LaeuftAbAmUtc);
    }

    public static string? GetBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
