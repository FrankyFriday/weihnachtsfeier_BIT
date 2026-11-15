using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class ChatHubAccessSynchronizer(
    WeihnachtsfeierDbContext dbContext,
    ProjectHubConnectionManager connectionManager)
{
    public async Task RefreshProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.PermissionsJson })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return;
        }

        var participants = await dbContext.Teilnehmer.AsNoTracking()
            .Include(item => item.Berechtigung)
            .Where(item => item.ProjektId == projectId)
            .ToListAsync(cancellationToken);

        var chatReaders = participants
            .Where(item => ParticipantPermissionKeys.ResolveEffectivePermissions(
                    project.PermissionsJson,
                    item.Berechtigung?.PermissionsJson)
                .GetValueOrDefault(ParticipantPermissionKeys.CanReadChat))
            .Select(item => item.Id)
            .ToHashSet();

        await connectionManager.UpdateChatReadersAsync(projectId, chatReaders, cancellationToken);
    }
}
