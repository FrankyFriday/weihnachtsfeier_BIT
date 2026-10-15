using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Hubs;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Services;

public interface IGameStateNotifier
{
    Task NotifyProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed class GameStateNotifier(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameStateHub> hubContext) : IGameStateNotifier
{
    public async Task NotifyProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();

        var project = await dbContext.Projekte.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null) return;

        var teams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ProjektId == projectId)
            .Include(t => t.Teilnehmer)
            .OrderByDescending(t => t.Punkte)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        var teamStates = new List<TeamState>();
        for (var i = 0; i < teams.Count; i++)
        {
            var team = teams[i];
            var members = team.Teilnehmer
                .OrderBy(m => m.Nummer)
                .Select(m => new ParticipantState(m.Id, m.Nummer, m.Name))
                .ToArray();

            teamStates.Add(new TeamState(
                team.Id,
                team.DisplayName,
                team.Color,
                team.Punkte,
                Rank: i + 1,
                ParticipantCount: members.Length,
                Members: members));
        }

        var gameState = new ProjectGameState(
            project.Id,
            project.Name,
            teamStates.ToArray(),
            DateTime.UtcNow);

        var groupName = GameStateHub.GetGroupName(projectId);
        await hubContext.Clients.Group(groupName).SendAsync("GameStateUpdated", gameState, cancellationToken);
    }
}
