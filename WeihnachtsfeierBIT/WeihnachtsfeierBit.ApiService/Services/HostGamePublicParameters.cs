using System.Text.Json;

namespace WeihnachtsfeierBit.ApiService.Services;

internal static class HostGamePublicParameters
{
    public static JsonElement Jeopardy(JsonElement parameters)
    {
        var state = ReadObject(parameters, "state");
        var used = ReadStringSet(state, "used");
        var active = ReadString(state, "active");
        var revealed = ReadBool(state, "revealed");
        var selectedTeamName = ReadString(state, "selectedTeamName");

        var categories = new List<object>();
        object? activeClue = null;
        if (parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("categories", out var categoryArray)
            && categoryArray.ValueKind == JsonValueKind.Array)
        {
            var categoryIndex = 0;
            foreach (var category in categoryArray.EnumerateArray())
            {
                var clues = new List<object>();
                if (category.ValueKind == JsonValueKind.Object
                    && category.TryGetProperty("clues", out var clueArray)
                    && clueArray.ValueKind == JsonValueKind.Array)
                {
                    var clueIndex = 0;
                    foreach (var clue in clueArray.EnumerateArray())
                    {
                        var key = $"{categoryIndex}-{clueIndex}";
                        var value = ReadInt(clue, "value");
                        clues.Add(new { value, used = used.Contains(key) });
                        if (key == active)
                        {
                            activeClue = new
                            {
                                category = ReadString(category, "name"),
                                value,
                                question = ReadString(clue, "question"),
                                answer = revealed ? ReadString(clue, "answer") : null,
                                selectedTeam = selectedTeamName
                            };
                        }

                        clueIndex++;
                    }
                }

                categories.Add(new { name = ReadString(category, "name"), clues });
                categoryIndex++;
            }
        }

        return JsonSerializer.SerializeToElement(new
        {
            title = ReadString(parameters, "title"),
            board = categories,
            activeClue
        });
    }

    private static JsonElement ReadObject(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static bool ReadBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static HashSet<string> ReadStringSet(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var array)
        && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToHashSet(StringComparer.Ordinal)
            : [];
}
