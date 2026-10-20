using Microsoft.AspNetCore.SignalR;

namespace WeihnachtsfeierBit.ApiService.Hubs;

public sealed class GameStateHub : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(projectId));
    }

    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(projectId));
    }

    public static string GetGroupName(Guid projectId) => $"project_{projectId}";
}
