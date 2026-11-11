using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed record PantomimeTeamAssignment(
    Guid TeamId,
    string TeamName,
    Guid PerformerId,
    string PerformerName);

public static class PantomimeGameService
{
    private static readonly string[] PublicStateProperties =
    [
        "isRunning",
        "isPaused",
        "isEnded",
        "startedAtUtc",
        "deadlineUtc",
        "remainingSeconds",
        "termsRemaining"
    ];

    private static readonly string[] PublicTeamProperties =
    [
        "teamId",
        "teamName",
        "performerId",
        "performerName",
        "correctCount"
    ];

    public static JsonElement PublicParameters(JsonElement parameters)
    {
        var result = new JsonObject();
        if (parameters.ValueKind == JsonValueKind.Object)
        {
            if (parameters.TryGetProperty("category", out var category) && category.ValueKind == JsonValueKind.String)
            {
                result["category"] = category.GetString();
            }

            if (parameters.TryGetProperty("roundDurationSeconds", out var duration) && duration.ValueKind == JsonValueKind.Number)
            {
                result["roundDurationSeconds"] = duration.GetInt32();
            }

            if (parameters.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                result["state"] = PublicState(state);
            }
        }

        return JsonSerializer.SerializeToElement(result);
    }

    public static JsonObject PublicState(JsonElement state)
    {
        var publicState = new JsonObject();
        foreach (var propertyName in PublicStateProperties)
        {
            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty(propertyName, out var value))
            {
                publicState[propertyName] = JsonNode.Parse(value.GetRawText());
            }
        }

        if (state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("teamStates", out var teamStates)
            && teamStates.ValueKind == JsonValueKind.Array)
        {
            var publicTeams = new JsonArray();
            foreach (var team in teamStates.EnumerateArray())
            {
                if (team.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var publicTeam = new JsonObject();
                foreach (var propertyName in PublicTeamProperties)
                {
                    if (team.TryGetProperty(propertyName, out var value))
                    {
                        publicTeam[propertyName] = JsonNode.Parse(value.GetRawText());
                    }
                }

                publicTeams.Add(publicTeam);
            }

            publicState["teamStates"] = publicTeams;
        }

        return publicState;
    }

    public static JsonObject CreateRoundState(
        IReadOnlyList<PantomimeTeamAssignment> assignments,
        int termsCount,
        int durationSeconds,
        DateTime startedAtUtc,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termsCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);

        var termOrder = Enumerable.Range(0, termsCount).ToArray();
        random ??= RandomNumberGeneratorWrapper.Instance;
        for (var index = termOrder.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (termOrder[index], termOrder[swapIndex]) = (termOrder[swapIndex], termOrder[index]);
        }

        var state = new JsonObject
        {
            ["isRunning"] = true,
            ["isPaused"] = false,
            ["isEnded"] = false,
            ["startedAtUtc"] = startedAtUtc,
            ["deadlineUtc"] = startedAtUtc.AddSeconds(durationSeconds),
            ["remainingSeconds"] = durationSeconds,
            ["termOrder"] = new JsonArray(termOrder.Select(index => JsonValue.Create(index)).ToArray()),
            ["nextTermPosition"] = 0,
            ["teamStates"] = new JsonArray(),
            ["termsRemaining"] = termsCount
        };

        var teamStates = (JsonArray)state["teamStates"]!;
        foreach (var assignment in assignments)
        {
            var teamState = new JsonObject
            {
                ["teamId"] = assignment.TeamId,
                ["teamName"] = assignment.TeamName,
                ["performerId"] = assignment.PerformerId,
                ["performerName"] = assignment.PerformerName,
                ["correctCount"] = 0,
                ["currentTermIndex"] = null,
                ["hasCurrentTerm"] = false
            };
            AssignNextTerm(state, teamState);
            teamStates.Add(teamState);
        }

        return state;
    }

    public static JsonObject? FindPerformerTeam(JsonObject state, Guid performerId) =>
        (state["teamStates"] as JsonArray)?
            .OfType<JsonObject>()
            .FirstOrDefault(team => ReadGuid(team, "performerId") == performerId);

    public static int? GetCurrentTermIndex(JsonObject teamState) =>
        teamState["currentTermIndex"] is JsonValue value && value.TryGetValue<int>(out var index)
            ? index
            : null;

    public static bool AssignNextTerm(JsonObject state, JsonObject teamState)
    {
        teamState["currentTermIndex"] = null;
        teamState["hasCurrentTerm"] = false;

        var termOrder = state["termOrder"] as JsonArray;
        var nextPosition = ReadInt(state, "nextTermPosition");
        if (termOrder is null || nextPosition < 0 || nextPosition >= termOrder.Count)
        {
            state["termsRemaining"] = 0;
            return false;
        }

        var nextTermIndex = termOrder[nextPosition]?.GetValue<int>();
        if (nextTermIndex is null)
        {
            return false;
        }

        teamState["currentTermIndex"] = nextTermIndex.Value;
        teamState["hasCurrentTerm"] = true;
        state["nextTermPosition"] = nextPosition + 1;
        state["termsRemaining"] = Math.Max(0, termOrder.Count - nextPosition - 1);
        return true;
    }

    public static bool ExpireIfNeeded(JsonObject state, DateTime nowUtc)
    {
        if (!ReadBool(state, "isRunning")
            || ReadDateTime(state, "deadlineUtc") is not DateTime deadline
            || deadline > nowUtc)
        {
            return false;
        }

        EndRound(state);
        return true;
    }

    public static void EndRound(JsonObject state)
    {
        state["isRunning"] = false;
        state["isPaused"] = false;
        state["isEnded"] = true;
        state["remainingSeconds"] = 0;
        if (state["teamStates"] is JsonArray teamStates)
        {
            foreach (var team in teamStates.OfType<JsonObject>())
            {
                team["currentTermIndex"] = null;
                team["hasCurrentTerm"] = false;
            }
        }
    }

    public static int RemainingSeconds(JsonObject state, DateTime nowUtc)
    {
        if (ReadBool(state, "isRunning") && ReadDateTime(state, "deadlineUtc") is DateTime deadline)
        {
            return Math.Max(0, (int)Math.Ceiling((deadline - nowUtc).TotalSeconds));
        }

        return Math.Max(0, ReadInt(state, "remainingSeconds"));
    }

    public static bool ReadBool(JsonObject state, string property) =>
        state[property] is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    public static int ReadInt(JsonObject state, string property) =>
        state[property] is JsonValue value && value.TryGetValue<int>(out var result) ? result : 0;

    public static Guid? ReadGuid(JsonObject state, string property) =>
        state[property] is JsonValue value && value.TryGetValue<Guid>(out var result) ? result : null;

    public static DateTime? ReadDateTime(JsonObject state, string property) =>
        state[property] is JsonValue value && value.TryGetValue<DateTime>(out var result) ? result : null;

    public static string ReadString(JsonObject state, string property) =>
        state[property] is JsonValue value && value.TryGetValue<string>(out var result) ? result ?? string.Empty : string.Empty;

    public static string[] GetDistinctTerms(JsonArray? terms) =>
        (terms ?? [])
            .Select(term => term?.GetValue<string>()?.Trim() ?? string.Empty)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static JsonObject? FindSlide(JsonArray slides, Guid slideId) =>
        slides.OfType<JsonObject>().FirstOrDefault(slide =>
            Guid.TryParse(slide["slideId"]?.GetValue<string>(), out var id) && id == slideId);

    private sealed class RandomNumberGeneratorWrapper : Random
    {
        public static RandomNumberGeneratorWrapper Instance { get; } = new();

        public override int Next(int maxValue) => RandomNumberGenerator.GetInt32(maxValue);
    }
}
