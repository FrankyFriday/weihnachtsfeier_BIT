using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.AdminClient.Models;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class SpontaneousEventModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }

    [Parameter] public FrameworkSelectOption[] SlideTypeOptions { get; set; } = [];
    [Parameter] public FrameworkSelectOption[] ModeOptions { get; set; } = [];
    [Parameter] public FrameworkSelectOption[] NavModeOptions { get; set; } = [];

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnEventCreated { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private string _spontaneousType = "announcement";
    private string _spontaneousMode = "active";
    private string _spontaneousTitleInput = string.Empty;
    private string _spontaneousSubtitleInput = string.Empty;
    private string _spontaneousNavMode = "host_controlled";
    private bool _spontaneousIsHidden;
    private bool _spontaneousIsScheduled;
    private string _spontaneousScheduledTimeInput = string.Empty;
    private bool _spontaneousAutoStart = true;
    private EditableSlide _spontaneousSlide = new(Guid.Empty, "announcement", 0, default);
    private string? _modalError;
    private bool _isProcessing;

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            _spontaneousType = "announcement";
            _spontaneousMode = "active";
            _spontaneousTitleInput = string.Empty;
            _spontaneousSubtitleInput = string.Empty;
            _spontaneousNavMode = "host_controlled";
            _spontaneousIsHidden = false;
            _spontaneousIsScheduled = false;
            _spontaneousScheduledTimeInput = string.Empty;
            _spontaneousAutoStart = true;
            _spontaneousSlide = new EditableSlide(Guid.Empty, _spontaneousType, 0, default);
            _modalError = null;
        }
    }

    private void OnSpontaneousTypeChanged(string? newType)
    {
        _spontaneousType = newType ?? "announcement";
        _spontaneousSlide = new EditableSlide(Guid.Empty, _spontaneousType, 0, default);
    }

    private async Task CreateSpontaneousEventWithEditorAsync()
    {
        var (parameters, error) = _spontaneousSlide.BuildParameters();
        if (error is not null)
        {
            _modalError = error;
            return;
        }

        _isProcessing = true;
        _modalError = null;

        DateTime? schedUtc = ParseTimeInput(_spontaneousIsScheduled, _spontaneousScheduledTimeInput);

        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.CreateSpontaneousEventAsync(
                        token,
                        ProjectId,
                        _spontaneousType,
                        _spontaneousMode,
                        parameters,
                        _spontaneousTitleInput,
                        _spontaneousSubtitleInput,
                        _spontaneousNavMode,
                        _spontaneousIsHidden,
                        schedUtc,
                        _spontaneousAutoStart);

                    await OnEventCreated.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _modalError = ex.Message;
        }
        finally
        {
            _isProcessing = false;
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
