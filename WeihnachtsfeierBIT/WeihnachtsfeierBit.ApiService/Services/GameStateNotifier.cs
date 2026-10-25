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
}

public sealed class GameStateNotifier(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameStateHub> hubContext) : IGameStateNotifier
{
    public async Task NotifyProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();

        var gameState = await BuildProjectGameStateAsync(projectId, dbContext, cancellationToken);
        if (gameState is null) return;

        var groupName = GameStateHub.GetGroupName(projectId);
        await hubContext.Clients.Group(groupName).SendAsync("GameStateUpdated", gameState, cancellationToken);
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
        var scheduledEvents = new List<EventState>();

        var instance = await dbContext.GameInstances.AsNoTracking()
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

            foreach (var passiveGie in liveEvents.Where(e => e.Mode == "passive" && !e.IsHidden))
            {
                passiveEvents.Add(ToEventState(passiveGie));
            }

            foreach (var schedGie in instance.Events.Where(e => e.Status == "scheduled" && !e.IsHidden).OrderBy(e => e.ScheduledStartUtc))
            {
                scheduledEvents.Add(ToEventState(schedGie));
            }
        }

        return new ProjectGameState(
            project.Id,
            project.Name,
            teamStates.ToArray(),
            activeEvent,
            passiveEvents.ToArray(),
            scheduledEvents.ToArray(),
            instance?.ActiveCanvasEventId,
            DateTime.UtcNow);
    }

    private static EventState ToEventState(GameInstanceEvent gie)
    {
        var slideStates = new List<EventSlideState>();
        try
        {
            using var doc = JsonDocument.Parse(gie.SlidesJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    Guid slideId = elem.TryGetProperty("slideId", out var idProp) && idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var parsedId) ? parsedId : Guid.NewGuid();
                    string type = elem.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String ? typeProp.GetString() ?? "announcement" : "announcement";
                    int pos = elem.TryGetProperty("position", out var posProp) && posProp.ValueKind == JsonValueKind.Number ? posProp.GetInt32() : 0;
                    JsonElement paramsElem = elem.TryGetProperty("parameters", out var pProp) ? pProp.Clone() : JsonSerializer.SerializeToElement(new { });

                    slideStates.Add(new EventSlideState(slideId, type, pos, paramsElem));
                }
            }
        }
        catch (JsonException)
        {
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
                    r.SlideId,
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
            gie.SourceShowId,
            gie.Title,
            gie.Mode,
            gie.Status,
            gie.NavigationMode,
            gie.CurrentSlideIndex,
            gie.IsHidden,
            gie.ScheduledStartUtc,
            gie.AutoStartOnSchedule,
            slideStates.ToArray(),
            responseStates,
            gie.StartedAtUtc);
    }
}
