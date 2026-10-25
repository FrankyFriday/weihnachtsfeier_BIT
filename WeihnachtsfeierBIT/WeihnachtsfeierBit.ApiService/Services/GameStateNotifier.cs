using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Hubs;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Services;

public interface IGameStateNotifier
{
    Task NotifyProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task NotifyCanvasSessionsEndedAsync(IEnumerable<string> accessCodes, CancellationToken cancellationToken = default);
}

public sealed class GameStateNotifier(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameStateHub> hubContext,
    IHubContext<CanvasHub> canvasHubContext) : IGameStateNotifier
{
    public async Task NotifyProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();

        var gameState = await BuildProjectGameStateAsync(projectId, dbContext, cancellationToken);
        if (gameState is null) return;

        var groupName = GameStateHub.GetGroupName(projectId);
        await hubContext.Clients.Group(groupName).SendAsync("GameStateUpdated", gameState, cancellationToken);

        var accessCode = await dbContext.GameInstances.AsNoTracking()
            .Where(instance => instance.ProjektId == projectId)
            .Select(instance => instance.AccessCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (accessCode is null)
        {
            return;
        }

        var canvasState = await CanvasStateBuilder.BuildAsync(accessCode, dbContext, cancellationToken);
        if (canvasState is not null)
        {
            await hubContext.Clients.Group(CanvasHub.GetGroupName(accessCode))
                .SendAsync("CanvasStateUpdated", canvasState, cancellationToken);
        }
    }

    public async Task NotifyCanvasSessionsEndedAsync(IEnumerable<string> accessCodes, CancellationToken cancellationToken = default)
    {
        foreach (var accessCode in accessCodes)
        {
            await canvasHubContext.Clients.Group(CanvasHub.GetGroupName(accessCode))
                .SendAsync("CanvasSessionEnded", cancellationToken);
        }
    }

    public static async Task<ProjectGameState?> BuildProjectGameStateAsync(Guid projectId, WeihnachtsfeierDbContext dbContext, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null) return null;

        var teams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ProjektId == projectId)
            .Include(t => t.Teilnehmer)
            .ToListAsync(cancellationToken);

        var orderedTeams = teams
            .OrderByDescending(t => t.Punkte)
            .ThenBy(t => t.DisplayName)
            .ToList();

        var teamStates = new List<TeamState>();
        for (var i = 0; i < orderedTeams.Count; i++)
        {
            var team = orderedTeams[i];
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

        EventState? activeEvent = null;
        var passiveEvents = new List<EventState>();
        var activeShows = new List<ActiveShowState>();

        var instance = await dbContext.GameInstances.AsNoTracking()
            .Include(gi => gi.Shows)
                .ThenInclude(gis => gis.Show)
                    .ThenInclude(s => s.Slides)
            .Include(gi => gi.Events)
                .ThenInclude(e => e.Responses)
                    .ThenInclude(r => r.Participant)
                        .ThenInclude(p => p.Team)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is not null)
        {
            var liveEvents = instance.Events
                .Where(e => e.Status == "active")
                .OrderByDescending(e => e.StartedAtUtc)
                .ToList();

            var activeGie = liveEvents.FirstOrDefault(e => e.Mode == "active");
            if (activeGie is not null)
            {
                activeEvent = ToEventState(activeGie);
            }

            foreach (var passiveGie in liveEvents.Where(e => e.Mode == "passive"))
            {
                passiveEvents.Add(ToEventState(passiveGie));
            }

            foreach (var gis in instance.Shows.OrderBy(s => s.CreatedAtUtc))
            {
                var currentShowEvent = liveEvents.FirstOrDefault(e => e.GameInstanceShowId == gis.Id);
                var totalSlides = gis.Show.Slides.Count;
                activeShows.Add(new ActiveShowState(
                    gis.Id,
                    gis.ShowId,
                    gis.Show.Name,
                    gis.Mode,
                    gis.CurrentSlideIndex,
                    totalSlides,
                    currentShowEvent?.Id));
            }
        }

        return new ProjectGameState(
            project.Id,
            project.Name,
            teamStates.ToArray(),
            activeEvent,
            passiveEvents.ToArray(),
            activeShows.ToArray(),
            instance?.ActiveCanvasEventId,
            DateTime.UtcNow);
    }

    private static EventState ToEventState(GameInstanceEvent gie)
    {
        JsonElement paramsElem;
        try
        {
            using var doc = JsonDocument.Parse(gie.ParametersJson);
            paramsElem = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            paramsElem = JsonSerializer.SerializeToElement(new { });
        }

        var responseStates = gie.Responses
            .OrderBy(r => r.SubmittedAtUtc)
            .Select(r =>
            {
                JsonElement responseData;
                try
                {
                    using var doc = JsonDocument.Parse(r.ResponseJson);
                    responseData = doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    responseData = JsonSerializer.SerializeToElement(new { });
                }

                return new EventResponseState(
                    r.Id,
                    r.ParticipantId,
                    r.Participant.Nummer,
                    r.Participant.Name,
                    r.Participant.Team?.DisplayName,
                    r.Participant.Team?.Color,
                    responseData,
                    r.SubmittedAtUtc);
            })
            .ToArray();

        return new EventState(
            gie.Id,
            gie.GameInstanceShowId,
            gie.SlideType,
            gie.Title,
            gie.Mode,
            gie.Status,
            paramsElem,
            gie.StartedAtUtc,
            responseStates);
    }
}
