using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

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
                activeEvent = new CanvasEventState(
                    gameEvent.Id,
                    gameEvent.SlideType,
                    gameEvent.Title,
                    BuildPublicParameters(gameEvent));
            }
        }

        return new CanvasGameState(
            instance.ProjektId,
            instance.Projekt.Name,
            teams,
            activeEvent,
            DateTime.UtcNow);
    }

    private static JsonElement BuildPublicParameters(GameInstanceEvent gameEvent)
    {
        var allowedProperties = gameEvent.SlideType.ToLowerInvariant() switch
        {
            "announcement" => new[] { "title", "message" },
            "poll" => new[] { "question", "options" },
            "quiz" => new[] { "question", "answers" },
            "estimation" => new[] { "question", "unit" },
            "buzzer" => new[] { "question" },
            _ => Array.Empty<string>()
        };

        using var parameters = JsonDocument.Parse(gameEvent.ParametersJson);
        var publicParameters = new Dictionary<string, JsonElement>();
        if (parameters.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in allowedProperties)
            {
                if (parameters.RootElement.TryGetProperty(propertyName, out var value))
                {
                    publicParameters[propertyName] = value.Clone();
                }
            }
        }

        return JsonSerializer.SerializeToElement(publicParameters);
    }
}
