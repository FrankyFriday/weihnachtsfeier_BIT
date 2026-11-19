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
                team.Color,
                team.Punkte,
                index + 1))
            .ToArray();

        CanvasEventState? activeEvent = null;
        Guid? activeTeamId = null;
        if (instance.ActiveCanvasEventId is Guid eventId)
        {
            var gameEvent = instance.Events.FirstOrDefault(item => item.Id == eventId);
            if (gameEvent is not null)
            {
                activeTeamId = QuestionWorkflowService.GetActiveTeamId(gameEvent);
                var currentSlide = GetCurrentSlide(gameEvent);
                if (currentSlide.HasValue)
                {
                    activeEvent = new CanvasEventState(
                        gameEvent.Id,
                        currentSlide.Value.Type,
                        gameEvent.Title,
                        BuildPublicParameters(currentSlide.Value.Type, currentSlide.Value.Parameters, teamParticipantIds),
                        currentSlide.Value.SlideId,
                        gameEvent.StartedAtUtc,
                        gameEvent.AccumulatedElapsedSeconds,
                        gameEvent.IsPaused,
                        gameEvent.LastResumedAtUtc);
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
            pointLogs,
            CanvasDisplaySettings.FromJson(instance.CanvasSettingsJson),
            activeTeamId);
    }

    private static (Guid SlideId, string Type, JsonElement Parameters)? GetCurrentSlide(GameInstanceEvent gameEvent)
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
                    var slideId = elem.TryGetProperty("slideId", out var slideIdProperty)
                        && slideIdProperty.ValueKind == JsonValueKind.String
                        && Guid.TryParse(slideIdProperty.GetString(), out var parsedSlideId)
                            ? parsedSlideId
                            : Guid.Empty;
                    string type = elem.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String ? typeProp.GetString() ?? "announcement" : "announcement";
                    JsonElement paramsElem = elem.TryGetProperty("parameters", out var pProp) ? pProp.Clone() : JsonSerializer.SerializeToElement(new { });
                    return (slideId, type, paramsElem);
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
            "poll" => new[] { "question", "options", "showPercentage" },
            "quiz" => new[] { "question", "answers", "questionTimeLimitSeconds", "timeLimitSeconds", "teamMode" },
            "estimation" => new[] { "question", "unit", "questionTimeLimitSeconds", "timeLimitSeconds", "teamMode" },
            "buzzer" => new[] { "question", "points", "timeLimitSeconds" },
            "ranking" => new[] { "title", "chartType" },
            "pointaward" => new[] { "title", "defaultReason", "defaultDeltaPoints" },
            "text" => new[] { "question", "placeholder", "maxLength" },
            "wordcloud" => new[] { "question", "maxWordsPerUser" },
            "slider" => new[] { "question", "min", "max", "step", "unit" },
            "rating" => new[] { "question", "maxStars" },
            "ordering" => new[] { "question", "items" },
            "countdown" => new[] { "title", "seconds", "message" },
            "hitster" => new[] { "title", "instructions", "cardsToWin", "game" },
            _ => Array.Empty<string>()
        };

        if (slideType.Equals("jeopardy", StringComparison.OrdinalIgnoreCase))
        {
            return HostGamePublicParameters.Jeopardy(parameters);
        }

        if (slideType.Equals("hitster", StringComparison.OrdinalIgnoreCase))
        {
            return HitsterGameService.PublicParameters(parameters, teamParticipantIds);
        }

        if (slideType.Equals("pantomime", StringComparison.OrdinalIgnoreCase))
        {
            return PantomimeGameService.PublicParameters(parameters);
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

            if (slideType.Equals("quiz", StringComparison.OrdinalIgnoreCase)
                || slideType.Equals("estimation", StringComparison.OrdinalIgnoreCase))
            {
                var workflow = ReadQuestionWorkflow(parameters);
                publicParameters["state"] = JsonSerializer.SerializeToElement(new
                {
                    questionWorkflow = new
                    {
                        phase = workflow.Phase,
                        timerEndsAtUtc = workflow.TimerEndsAtUtc,
                        timerPaused = workflow.TimerPaused,
                        remainingSeconds = workflow.RemainingSeconds,
                        timerDurationSeconds = workflow.TimerDurationSeconds,
                        officialAnswerIndex = workflow.OfficialAnswerIndex,
                        officialEstimationValue = workflow.OfficialEstimationValue
                    }
                });
                if (string.Equals(workflow.Phase, QuestionWorkflowService.ResultsPhase, StringComparison.OrdinalIgnoreCase))
                {
                    var solutionProperty = slideType.Equals("quiz", StringComparison.OrdinalIgnoreCase)
                        ? "correctAnswerIndices"
                        : "correctValue";
                    if (parameters.TryGetProperty(solutionProperty, out var solution))
                    {
                        publicParameters[solutionProperty] = solution.Clone();
                    }
                    if (parameters.TryGetProperty("explanation", out var explanation))
                    {
                        publicParameters["explanation"] = explanation.Clone();
                    }
                }
            }
        }

        return JsonSerializer.SerializeToElement(publicParameters);
    }

    private static QuestionWorkflowViewState ReadQuestionWorkflow(JsonElement parameters)
    {
        var workflow = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("state", out var state)
            && state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("questionWorkflow", out var configuredWorkflow)
            && configuredWorkflow.ValueKind == JsonValueKind.Object
                ? configuredWorkflow
                : default;
        var phase = workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("phase", out var phaseValue)
            && phaseValue.ValueKind == JsonValueKind.String
                ? phaseValue.GetString()
                : null;
        var endsAt = workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("timerEndsAtUtc", out var endValue)
            && endValue.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(endValue.GetString(), out var parsedEnd)
                ? parsedEnd
                : (DateTimeOffset?)null;
        var paused = workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("timerPaused", out var pausedValue)
            && pausedValue.ValueKind == JsonValueKind.True;
        var remaining = workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("remainingSeconds", out var remainingValue)
            && remainingValue.ValueKind == JsonValueKind.Number
            && remainingValue.TryGetInt32(out var seconds)
                ? seconds
                : 0;
        var isResultsPhase = string.Equals(phase, QuestionWorkflowService.ResultsPhase, StringComparison.OrdinalIgnoreCase);
        var officialAnswer = isResultsPhase
            && workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("officialAnswerIndex", out var answerValue)
            && answerValue.ValueKind == JsonValueKind.Number
            && answerValue.TryGetInt32(out var answerIndex)
                ? answerIndex
                : (int?)null;
        var officialEstimation = isResultsPhase
            && workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("officialEstimationValue", out var estimateValue)
            && estimateValue.ValueKind == JsonValueKind.Number
            && estimateValue.TryGetDouble(out var estimate)
                ? estimate
                : (double?)null;

        return new QuestionWorkflowViewState(
            phase ?? QuestionWorkflowService.QuestionPhase,
            endsAt,
            paused,
            remaining,
            officialAnswer,
            officialEstimation,
            workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("timerDurationSeconds", out var durationValue)
            && durationValue.ValueKind == JsonValueKind.Number
            && durationValue.TryGetInt32(out var durationSeconds)
                ? durationSeconds
                : 0);
    }
}
