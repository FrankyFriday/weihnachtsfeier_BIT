using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Hubs;

public sealed class GameStateHub(
    ProjectAccessService accessService,
    ProjectHubConnectionManager connectionManager,
    WeihnachtsfeierDbContext dbContext) : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        var request = Context.GetHttpContext()?.Request;
        var token = request is null ? null : ProjectAccessService.GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(token) &&
            request?.Query.TryGetValue("access_token", out var queryToken) == true)
        {
            token = queryToken.ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new HubException("Kein Zugriffstoken an den Hub übergeben.");
        }

        var access = await accessService.AuthenticateAsync(projectId, token, Context.ConnectionAborted);
        if (access is null)
        {
            throw new HubException("Keine gültige Projektsitzung.");
        }

        var canReadChat = true;
        if (access.ParticipantId is Guid participantId)
        {
            var participant = await dbContext.Teilnehmer.AsNoTracking()
                .Where(item => item.Id == participantId && item.ProjektId == projectId)
                .Select(item => new { item.Berechtigung!.PermissionsJson })
                .FirstOrDefaultAsync(Context.ConnectionAborted);
            if (participant is null)
            {
                throw new HubException("Kein Teilnehmerzugriff für dieses Projekt.");
            }

            var globalPermissions = await dbContext.Projekte.AsNoTracking()
                .Where(item => item.Id == projectId)
                .Select(item => item.PermissionsJson)
                .FirstOrDefaultAsync(Context.ConnectionAborted);
            var effectivePermissions = ParticipantPermissionKeys.ResolveEffectivePermissions(
                globalPermissions,
                participant?.PermissionsJson);
            canReadChat = effectivePermissions.GetValueOrDefault(ParticipantPermissionKeys.CanReadChat);
        }

        var joined = await connectionManager.JoinProjectAsync(
            access,
            projectId,
            Context.ConnectionId,
            canReadChat,
            Context.ConnectionAborted);
        if (!joined)
        {
            throw new HubException("Die Sitzung wurde beendet.");
        }
    }

    public async Task LeaveProject(Guid projectId)
    {
        await connectionManager.LeaveProjectAsync(Context.ConnectionId, projectId, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await connectionManager.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task Ping() => Task.CompletedTask;

    public static string GetGroupName(Guid projectId) => $"project_{projectId}";
    public static string GetChatGroupName(Guid projectId) => $"project_{projectId}_chat";
}
