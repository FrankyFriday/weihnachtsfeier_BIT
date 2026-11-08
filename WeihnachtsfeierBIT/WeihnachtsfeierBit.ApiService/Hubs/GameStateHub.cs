using Microsoft.AspNetCore.SignalR;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Services;

namespace WeihnachtsfeierBit.ApiService.Hubs;

public sealed class GameStateHub(
    ProjectAccessService accessService,
    ProjectHubConnectionManager connectionManager) : Hub
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

        var access = await accessService.AuthenticateAsync(projectId, token, Context.ConnectionAborted);
        if (access is null)
        {
            throw new HubException("Keine gültige Projektsitzung.");
        }

        var joined = await connectionManager.JoinProjectAsync(
            access.SessionId,
            projectId,
            Context.ConnectionId,
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
}
