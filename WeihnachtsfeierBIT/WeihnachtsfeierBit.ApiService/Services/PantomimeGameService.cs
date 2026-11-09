using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeihnachtsfeierBit.ApiService.Services;

public static class PantomimeGameService
{
    private static readonly string[] PublicStateProperties =
    [
        "teamId",
        "teamName",
        "performerId",
        "performerName",
        "termIndex",
        "correctCount",
        "isRunning",
        "isPaused",
        "deadlineUtc",
        "remainingSeconds"
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
                var publicState = new JsonObject();
                foreach (var propertyName in PublicStateProperties)
                {
                    if (state.TryGetProperty(propertyName, out var value))
                    {
                        publicState[propertyName] = JsonNode.Parse(value.GetRawText());
                    }
                }

                result["state"] = publicState;
            }
        }

        return JsonSerializer.SerializeToElement(result);
    }

    public static JsonObject? FindSlide(JsonArray slides, Guid slideId) =>
        slides.OfType<JsonObject>().FirstOrDefault(slide =>
            Guid.TryParse(slide["slideId"]?.GetValue<string>(), out var id) && id == slideId);
}
