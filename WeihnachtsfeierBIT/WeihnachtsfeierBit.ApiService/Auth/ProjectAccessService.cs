using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class ProjectAccessService(WeihnachtsfeierDbContext dbContext)
{
    private static readonly TimeSpan ActivityUpdateInterval = TimeSpan.FromSeconds(30);

    public async Task<ProjectAccess?> AuthenticateAsync(Guid projectId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var tokenHash = ApiToken.Hash(token);
        var adminSession = await dbContext.ProjektAdminSitzungen
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash &&
            session.ProjektAdmin.Projekte.Any(membership => membership.ProjektId == projectId) &&
                session.WiderrufenAmUtc == null &&
                session.LaeuftAbAmUtc > now, cancellationToken);

        if (adminSession is not null)
        {
            if (now - adminSession.LetzteAktivitaetAmUtc >= ActivityUpdateInterval)
            {
                adminSession.LetzteAktivitaetAmUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new ProjectAccess(projectId, null, adminSession.ProjektAdminId);
        }

        var participantSession = await dbContext.Sitzungen
            .Include(session => session.Teilnehmer)
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash &&
                session.Teilnehmer.ProjektId == projectId &&
                session.WiderrufenAmUtc == null &&
                session.LaeuftAbAmUtc > now, cancellationToken);

        if (participantSession is null)
        {
            return null;
        }

        if (now - participantSession.LetzteAktivitaetAmUtc >= ActivityUpdateInterval)
        {
            participantSession.LetzteAktivitaetAmUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProjectAccess(projectId, participantSession.TeilnehmerId, null);
    }

    public async Task RevokeParticipantSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var tokenHash = ApiToken.Hash(token);
        var session = await dbContext.Sitzungen.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.WiderrufenAmUtc == null, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAdminSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var tokenHash = ApiToken.Hash(token);
        var session = await dbContext.ProjektAdminSitzungen.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.WiderrufenAmUtc == null, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
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