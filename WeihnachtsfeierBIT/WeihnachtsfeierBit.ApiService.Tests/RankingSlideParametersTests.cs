using System.Text.Json;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class RankingSlideParametersTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("bar_chart")]
    [InlineData("line_graph")]
    public void ChartType_SurvivesJsonRoundTrip(string chartType)
    {
        var parameters = new RankingSlideParameters { ChartType = chartType };

        var json = JsonSerializer.SerializeToElement(parameters, JsonOptions);
        var reloaded = json.Deserialize<RankingSlideParameters>(JsonOptions);

        Assert.NotNull(reloaded);
        Assert.Equal(chartType, reloaded.ChartType);
    }
}
