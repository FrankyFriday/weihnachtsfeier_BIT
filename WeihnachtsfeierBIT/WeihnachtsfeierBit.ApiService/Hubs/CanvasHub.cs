using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Hubs;

public sealed class CanvasHub(WeihnachtsfeierDbContext dbContext) : Hub
{
    public async Task JoinCanvas(string accessCode)
    {
        var code = accessCode.Trim();
        if (!CanvasStateBuilder.IsValidAccessCode(code) ||
            !await dbContext.GameInstances.AnyAsync(instance => instance.AccessCode == code))
        {
            throw new HubException("Der Session-Code ist ungültig oder abgelaufen.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(code));
    }

    public Task LeaveCanvas(string accessCode) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(accessCode.Trim()));

    public static string GetGroupName(string accessCode) => $"canvas_{accessCode}";
}
