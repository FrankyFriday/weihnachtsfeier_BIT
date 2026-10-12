using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class AdminSessionAccessService(WeihnachtsfeierDbContext dbContext)
{
    public async Task<Guid?> GetAdminIdAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = ApiToken.Hash(token);
        var now = DateTime.UtcNow;
        return await dbContext.ProjektAdminSitzungen.AsNoTracking()
            .Where(session => session.TokenHash == tokenHash &&
                session.WiderrufenAmUtc == null && session.LaeuftAbAmUtc > now)
            .Select(session => (Guid?)session.ProjektAdminId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}