using System.Text.Json;
using System.Text.Json.Nodes;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Services;

public static class QuestionWorkflowService
{
    public const string QuestionPhase = "question";
    public const string AnswerPhase = "answers";
    public const string LockedPhase = "locked";
    public const string ResultsPhase = "results";

    public static bool IsQuestionSlide(JsonObject slide) =>
        string.Equals(ReadString(slide, "type"), "quiz", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadString(slide, "type"), "estimation", StringComparison.OrdinalIgnoreCase);

    public static bool IsTeamExclusive(JsonObject slide) =>
        string.Equals(ReadString(GetParameters(slide), "teamMode"), "team_exclusive", StringComparison.OrdinalIgnoreCase);

    public static bool CanEditTeamResponse(string slideType, Guid participantId, Guid targetParticipantId) =>
        !string.Equals(slideType, "quiz", StringComparison.OrdinalIgnoreCase)
        || participantId == targetParticipantId;

    public static bool HasOfficialTeamResult(string slideType, JsonObject workflow)
    {
        if (string.Equals(slideType, "quiz", StringComparison.OrdinalIgnoreCase))
        {
            return ReadInt(workflow, "officialAnswerIndex").HasValue;
        }

        return string.Equals(slideType, "estimation", StringComparison.OrdinalIgnoreCase)
            && workflow["officialEstimationValue"] is JsonValue jsonValue
            && jsonValue.TryGetValue<double>(out var estimate)
            && double.IsFinite(estimate);
    }

    public static ProjectGameState FilterProjectState(ProjectGameState state, Guid? recipientTeamId) =>
        state with
        {
            ActiveEvent = FilterEventResponses(state.ActiveEvent, recipientTeamId),
            PassiveEvents = state.PassiveEvents.Select(item => FilterEventResponses(item, recipientTeamId)!).ToArray(),
            ScheduledEvents = state.ScheduledEvents.Select(item => FilterEventResponses(item, recipientTeamId)!).ToArray()
        };

    public static JsonObject GetParameters(JsonObject slide) =>
        slide["parameters"] as JsonObject ?? new JsonObject();

    public static JsonObject GetWorkflowState(JsonObject slide)
    {
        var parameters = GetParameters(slide);
        var state = parameters["state"] as JsonObject;
        if (state is null)
        {
            state = new JsonObject();
            parameters["state"] = state;
            slide["parameters"] = parameters;
        }

        var workflow = state["questionWorkflow"] as JsonObject;
        if (workflow is null)
        {
            workflow = new JsonObject { ["phase"] = QuestionPhase };
            state["questionWorkflow"] = workflow;
        }

        return workflow;
    }

    public static string GetPhase(JsonObject slide)
    {
        var parameters = GetParameters(slide);
        var state = parameters["state"] as JsonObject;
        var workflow = state?["questionWorkflow"] as JsonObject;
        return ReadString(workflow, "phase") is { Length: > 0 } phase ? phase : QuestionPhase;
    }

    public static int? GetConfiguredSeconds(JsonObject slide, string propertyName)
    {
        var parameters = GetParameters(slide);
        return ReadInt(parameters, propertyName);
    }

    public static DateTimeOffset? GetTimerEnd(JsonObject slide)
    {
        var workflow = GetExistingWorkflowState(slide);
        var value = ReadString(workflow, "timerEndsAtUtc");
        return DateTimeOffset.TryParse(value, out var end) ? end.ToUniversalTime() : null;
    }

    public static int GetRemainingSeconds(JsonObject slide, DateTimeOffset now)
    {
        var workflow = GetExistingWorkflowState(slide);
        if (workflow is null)
        {
            return 0;
        }

        if (ReadBool(workflow, "timerPaused"))
        {
            return Math.Max(0, ReadInt(workflow, "remainingSeconds") ?? 0);
        }

        var end = GetTimerEnd(slide);
        return end.HasValue ? Math.Max(0, (int)Math.Ceiling((end.Value - now).TotalSeconds)) : 0;
    }

    public static bool TickActiveSlide(JsonObject slide, DateTimeOffset now)
    {
        if (!IsQuestionSlide(slide))
        {
            return false;
        }

        var workflow = GetWorkflowState(slide);
        var phase = ReadString(workflow, "phase");
        if (string.IsNullOrEmpty(phase))
        {
            phase = QuestionPhase;
            workflow["phase"] = phase;
        }

        if (phase == QuestionPhase && ReadString(workflow, "phaseStartedAtUtc").Length == 0)
        {
            StartPhase(slide, QuestionPhase, GetConfiguredSeconds(slide, "questionTimeLimitSeconds"), now);
            workflow = GetWorkflowState(slide);
        }

        if (ReadBool(workflow, "timerPaused"))
        {
            return false;
        }

        var timerEnd = GetTimerEnd(slide);
        if (!timerEnd.HasValue || timerEnd.Value > now)
        {
            return false;
        }

        if (phase == QuestionPhase)
        {
            StartPhase(slide, AnswerPhase, GetConfiguredSeconds(slide, "timeLimitSeconds"), now);
            return true;
        }

        if (phase == AnswerPhase)
        {
            workflow["phase"] = LockedPhase;
            workflow["phaseStartedAtUtc"] = now.ToString("O");
            workflow["timerEndsAtUtc"] = null;
            workflow["timerPaused"] = false;
            workflow["remainingSeconds"] = 0;
            return true;
        }

        return false;
    }

    public static void StartPhase(JsonObject slide, string phase, int? durationSeconds, DateTimeOffset now)
    {
        var workflow = GetWorkflowState(slide);
        workflow["phase"] = phase;
        workflow["phaseStartedAtUtc"] = now.ToString("O");
        workflow["timerEndsAtUtc"] = durationSeconds is > 0
            ? now.AddSeconds(durationSeconds.Value).ToString("O")
            : null;
        workflow["timerPaused"] = false;
        workflow["remainingSeconds"] = durationSeconds ?? 0;
    }

    public static Guid? GetActiveTeamId(GameInstanceEvent gameEvent)
    {
        try
        {
            using var document = JsonDocument.Parse(gameEvent.SlidesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var slide in document.RootElement.EnumerateArray())
            {
                if (!slide.TryGetProperty("parameters", out var parameters)
                    || parameters.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("state", out var state)
                    || state.ValueKind != JsonValueKind.Object
                    || !state.TryGetProperty("selectedTeamId", out var teamId)
                    || teamId.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (Guid.TryParse(teamId.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static JsonObject? GetExistingWorkflowState(JsonObject slide)
    {
        var parameters = GetParameters(slide);
        var state = parameters["state"] as JsonObject;
        return state?["questionWorkflow"] as JsonObject;
    }

    private static EventState? FilterEventResponses(EventState? gameEvent, Guid? recipientTeamId)
    {
        if (gameEvent is null)
        {
            return null;
        }

        var exclusiveSlides = gameEvent.Slides
            .Where(slide => slide.Parameters.ValueKind == JsonValueKind.Object
                && slide.Parameters.TryGetProperty("teamMode", out var teamMode)
                && teamMode.ValueKind == JsonValueKind.String
                && string.Equals(teamMode.GetString(), "team_exclusive", StringComparison.OrdinalIgnoreCase))
            .Select(slide => slide.SlideId)
            .ToHashSet();
        var sanitizedSlides = gameEvent.Slides.Select(FilterQuestionSlide).ToArray();

        if (exclusiveSlides.Count == 0)
        {
            return gameEvent with { Slides = sanitizedSlides };
        }

        var visibleResponses = gameEvent.Responses
            .Where(response => !exclusiveSlides.Contains(response.SlideId)
                || (recipientTeamId.HasValue
                    && gameEvent.ActiveTeamId == recipientTeamId
                    && response.TeamId == recipientTeamId))
            .ToArray();
        return gameEvent with { Responses = visibleResponses, Slides = sanitizedSlides };
    }

    private static EventSlideState FilterQuestionSlide(EventSlideState slide)
    {
        if (slide.Parameters.ValueKind != JsonValueKind.Object)
        {
            return slide;
        }

        var isQuestionSlide = string.Equals(slide.Type, "quiz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slide.Type, "estimation", StringComparison.OrdinalIgnoreCase);
        var hasModeratorNotes = slide.Parameters.TryGetProperty("adminNotes", out _);
        if (!isQuestionSlide && !hasModeratorNotes)
        {
            return slide;
        }

        var workflow = isQuestionSlide ? ReadWorkflowFromElement(slide.Parameters) : null;
        if (JsonNode.Parse(slide.Parameters.GetRawText()) is not JsonObject parameters)
        {
            return slide;
        }

        parameters.Remove("adminNotes");
        if (!isQuestionSlide)
        {
            return slide with { Parameters = JsonSerializer.SerializeToElement(parameters) };
        }

        var isResultsPhase = string.Equals(workflow!.Phase, ResultsPhase, StringComparison.OrdinalIgnoreCase);
        if (!isResultsPhase && string.Equals(slide.Type, "quiz", StringComparison.OrdinalIgnoreCase))
        {
            parameters.Remove("correctAnswerIndices");
        }
        else if (!isResultsPhase)
        {
            parameters.Remove("correctValue");
        }

        if (!isResultsPhase)
        {
            parameters.Remove("explanation");
        }

        if (parameters["state"] is JsonObject state
            && state["questionWorkflow"] is JsonObject questionWorkflow)
        {
            questionWorkflow.Remove("scoreAwards");
            if (!isResultsPhase)
            {
                questionWorkflow.Remove("officialAnswerIndex");
                questionWorkflow.Remove("officialEstimationValue");
            }
        }

        return slide with { Parameters = JsonSerializer.SerializeToElement(parameters) };
    }

    private static QuestionWorkflowViewState ReadWorkflowFromElement(JsonElement parameters)
    {
        var state = parameters.TryGetProperty("state", out var stateValue) ? stateValue : default;
        var workflow = state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("questionWorkflow", out var workflowValue)
                ? workflowValue
                : default;
        var phase = workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("phase", out var phaseValue)
            && phaseValue.ValueKind == JsonValueKind.String
                ? phaseValue.GetString()
                : null;
        return new QuestionWorkflowViewState(
            phase ?? QuestionPhase,
            null,
            false,
            0,
            null,
            null);
    }

    private static string ReadString(JsonObject? value, string propertyName) =>
        value?[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number)
            ? number
            : null;

    private static bool ReadBool(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;
}
