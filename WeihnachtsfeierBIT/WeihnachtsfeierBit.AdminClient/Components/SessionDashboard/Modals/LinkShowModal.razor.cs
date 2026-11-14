using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class LinkShowModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public ProjectShow[] Shows { get; set; } = [];

    [Parameter] public FrameworkSelectOption[] ModeOptions { get; set; } = [];
    [Parameter] public FrameworkSelectOption[] NavModeOptions { get; set; } = [];

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnShowStarted { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private string _showStartMode = "active";
    private string _showTitleInput = string.Empty;
    private string _showSubtitleInput = string.Empty;
    private string _showNavMode = "host_controlled";
    private bool _showIsHidden;
    private bool _showIsScheduled;
    private string _showScheduledTimeInput = string.Empty;
    private bool _showAutoStart = true;

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            _showStartMode = "active";
            _showTitleInput = string.Empty;
            _showSubtitleInput = string.Empty;
            _showNavMode = "host_controlled";
            _showIsHidden = false;
            _showIsScheduled = false;
            _showScheduledTimeInput = string.Empty;
            _showAutoStart = true;
        }
    }

    private async Task StartShowAndCloseModalAsync(Guid showId)
    {
        DateTime? schedUtc = ParseTimeInput(_showIsScheduled, _showScheduledTimeInput);

        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.StartSessionShowAsync(token, ProjectId, showId, _showStartMode, _showTitleInput, _showSubtitleInput, _showNavMode, _showIsHidden, schedUtc, _showAutoStart);
                    await OnShowStarted.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch
        {
        }
    }

    private static DateTime? ParseTimeInput(bool isScheduled, string input)
    {
        if (!isScheduled || string.IsNullOrWhiteSpace(input)) return null;

        var parts = input.Trim().Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Local);
            if (target < now)
            {
                target = target.AddDays(1);
            }
            return target.ToUniversalTime();
        }

        return null;
    }
}
