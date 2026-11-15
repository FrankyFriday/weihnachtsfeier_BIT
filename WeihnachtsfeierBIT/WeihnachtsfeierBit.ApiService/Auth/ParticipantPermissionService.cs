using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class ParticipantPermissionService(WeihnachtsfeierDbContext dbContext)
{
    public async Task<bool> AllowsAsync(
        ProjectAccess access,
        string permissionKey,
        bool requireInteraction,
        CancellationToken cancellationToken)
    {
        if (access.ParticipantId is not Guid participantId)
        {
            return access.IsAdmin;
        }

        var participant = await dbContext.Teilnehmer.AsNoTracking()
            .Include(item => item.Berechtigung)
            .FirstOrDefaultAsync(
                item => item.Id == participantId && item.ProjektId == access.ProjectId,
                cancellationToken);
        if (participant is null)
        {
            return false;
        }

        var projectPermissions = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == access.ProjectId)
            .Select(item => item.PermissionsJson)
            .FirstOrDefaultAsync(cancellationToken);
        var effectivePermissions = ParticipantPermissionKeys.ResolveEffectivePermissions(
            projectPermissions,
            participant.Berechtigung?.PermissionsJson);

        if (requireInteraction &&
            !effectivePermissions.GetValueOrDefault(ParticipantPermissionKeys.IsMuted))
        {
            return false;
        }

        return effectivePermissions.GetValueOrDefault(permissionKey);
    }
}
