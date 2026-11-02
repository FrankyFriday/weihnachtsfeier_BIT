using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Endpoints;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;
using CanvasPointLogDto = WeihnachtsfeierBit.Framework.TeamPointLogDto;

namespace WeihnachtsfeierBit.ApiService.Services;

public static class CanvasStateBuilder
{
    public static bool IsValidAccessCode(string code) =>
        code is { Length: 6 } && code.All(char.IsAsciiDigit);

    public static async Task<CanvasGameState?> BuildAsync(
        string accessCode,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsValidAccessCode(accessCode))
        {
            return null;
        }

        var instance = await dbContext.GameInstances.AsNoTracking()
            .Include(gameInstance => gameInstance.Projekt)
            .Include(gameInstance => gameInstance.Events.Where(gameEvent => gameEvent.Status == "active"))
            .FirstOrDefaultAsync(gameInstance => gameInstance.AccessCode == accessCode, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        var orderedTeams = await dbContext.Teams.AsNoTracking()
            .Where(team => team.ProjektId == instance.ProjektId)
            .OrderByDescending(team => team.Punkte)
            .ThenBy(team => team.DisplayName)
            .Select(team => new
            {
                team.Id,
                team.DisplayName,
                team.Color,
                team.Punkte
            })
            .ToListAsync(cancellationToken);
        var teamParticipantRows = await dbContext.Teilnehmer.AsNoTracking()
            .Where(participant => participant.ProjektId == instance.ProjektId && participant.TeamId != null)
            .Select(participant => new { participant.Id, participant.TeamId })
            .ToListAsync(cancellationToken);
        var teamParticipantIds = teamParticipantRows
            .GroupBy(participant => participant.TeamId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(participant => participant.Id).ToHashSet());

        var teams = orderedTeams
            .Select((team, index) => new CanvasTeamState(
                team.Id,
                team.DisplayName,
                team.Punkte,
                index + 1))
            .ToArray();

        CanvasEventState? activeEvent = null;
        if (instance.ActiveCanvasEventId is Guid eventId)
        {
            var gameEvent = instance.Events.FirstOrDefault(item => item.Id == eventId);
            if (gameEvent is not null)
            {
                var currentSlide = GetCurrentSlide(gameEvent);
                if (currentSlide.HasValue)
                {
                    activeEvent = new CanvasEventState(
                        gameEvent.Id,
                        currentSlide.Value.Type,
                        gameEvent.Title,
                        BuildPublicParameters(currentSlide.Value.Type, currentSlide.Value.Parameters, teamParticipantIds));
                }
            }
        }

        var pointLogs = Array.Empty<CanvasPointLogDto>();
        if (activeEvent is not null &&
            activeEvent.SlideType.Equals("ranking", StringComparison.OrdinalIgnoreCase) &&
            activeEvent.Parameters.TryGetProperty("chartType", out var chartType) &&
            chartType.ValueKind == JsonValueKind.String &&
            chartType.GetString() == "line_graph")
        {
            pointLogs = await dbContext.TeamPointLogs.AsNoTracking()
                .Where(log => log.ProjektId == instance.ProjektId)
                .OrderByDescending(log => log.CreatedAtUtc)
                .Select(log => new CanvasPointLogDto(
                    log.Id,
                    log.TeamId,
                    log.Team.DisplayName,
                    log.Team.Color,
                    log.DeltaPoints,
                    log.NewTotalPoints,
                    log.Reason,
                    log.CreatedAtUtc))
                .ToArrayAsync(cancellationToken);
        }

        return new CanvasGameState(
            instance.ProjektId,
            instance.Projekt.Name,
            teams,
            activeEvent,
            DateTime.UtcNow,
            pointLogs);
    }

    private static (string Type, JsonElement Parameters)? GetCurrentSlide(GameInstanceEvent gameEvent)
    {
        try
        {
            using var doc = JsonDocument.Parse(gameEvent.SlidesJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var array = doc.RootElement.EnumerateArray().ToList();
                int idx = Math.Clamp(gameEvent.CurrentSlideIndex, 0, Math.Max(0, array.Count - 1));
                if (array.Count > idx)
                {
                    var elem = array[idx];
                    string type = elem.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String ? typeProp.GetString() ?? "announcement" : "announcement";
                    JsonElement paramsElem = elem.TryGetProperty("parameters", out var pProp) ? pProp.Clone() : JsonSerializer.SerializeToElement(new { });
                    return (type, paramsElem);
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static JsonElement BuildPublicParameters(
        string slideType,
        JsonElement parameters,
        IReadOnlyDictionary<Guid, HashSet<Guid>> teamParticipantIds)
    {
        var allowedProperties = slideType.ToLowerInvariant() switch
        {
            "announcement" => new[] { "title", "message" },
            "poll" => new[] { "question", "options" },
            "quiz" => new[] { "question", "answers" },
            "estimation" => new[] { "question", "unit" },
            "buzzer" => new[] { "question" },
            "ranking" => new[] { "chartType" },
            "hitster" => new[] { "title", "instructions", "cardsToWin", "game" },
            _ => Array.Empty<string>()
        };

        if (slideType.Equals("hitster", StringComparison.OrdinalIgnoreCase))
        {
            return HitsterGameService.PublicParameters(parameters, teamParticipantIds);
        }

        var publicParameters = new Dictionary<string, JsonElement>();
        if (parameters.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in allowedProperties)
            {
                if (parameters.TryGetProperty(propertyName, out var value))
                {
                    publicParameters[propertyName] = value.Clone();
                }
            }
        }

        return JsonSerializer.SerializeToElement(publicParameters);
    }
}
