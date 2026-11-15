using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class CanvasDisplaySettingsTests
{
    [Theory]
    [InlineData(CanvasDisplaySettings.ScoreboardRight, CanvasDisplaySettings.QrCodeCard)]
    [InlineData(CanvasDisplaySettings.ScoreboardBottom, CanvasDisplaySettings.QrCodePopup)]
    [InlineData(CanvasDisplaySettings.ScoreboardHidden, CanvasDisplaySettings.QrCodeHidden)]
    public void CanvasDisplaySettings_RoundTripSupportedModes(string scoreboardLayout, string qrCodeMode)
    {
        var settings = new CanvasDisplaySettings(scoreboardLayout, qrCodeMode);

        var restored = CanvasDisplaySettings.FromJson(settings.ToJson());

        Assert.True(settings.IsValid);
        Assert.Equal(settings, restored);
    }

    [Fact]
    public void CanvasDisplaySettings_RejectsUnknownModes()
    {
        var settings = new CanvasDisplaySettings("left", "floating");

        Assert.False(settings.IsValid);
    }

    [Fact]
    public void CanvasDisplaySettings_UsesDefaultScalesForLegacyJson()
    {
        var settings = CanvasDisplaySettings.FromJson(
            """{"scoreboardLayout":"right","qrCodeMode":"card"}""");

        Assert.Equal(CanvasDisplaySettings.Default, settings);
        Assert.True(settings.IsValid);
    }

    [Fact]
    public void CanvasDisplaySettings_RoundTripsCustomizedScales()
    {
        var settings = CanvasDisplaySettings.Default with
        {
            FontScalePercent = 120,
            EventScalePercent = 80,
            ScoreboardScalePercent = 140,
            QrCodeScalePercent = 90
        };

        Assert.Equal(settings, CanvasDisplaySettings.FromJson(settings.ToJson()));
    }

    [Theory]
    [InlineData(CanvasDisplaySettings.MinimumScalePercent)]
    [InlineData(CanvasDisplaySettings.MaximumScalePercent)]
    public void CanvasDisplaySettings_AcceptsScaleRangeBoundaries(int scalePercent)
    {
        var settings = CanvasDisplaySettings.Default with
        {
            FontScalePercent = scalePercent
        };

        Assert.True(settings.IsValid);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(161)]
    public void CanvasDisplaySettings_RejectsScalesOutsideSupportedRange(int scalePercent)
    {
        var settings = CanvasDisplaySettings.Default with
        {
            FontScalePercent = scalePercent
        };

        Assert.False(settings.IsValid);
    }
}
