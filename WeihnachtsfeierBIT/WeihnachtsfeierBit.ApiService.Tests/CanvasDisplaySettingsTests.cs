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
}
