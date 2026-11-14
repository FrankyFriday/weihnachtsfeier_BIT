using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class EditEventModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public EventState? EditingEvent { get; set; }

    [Parameter] public FrameworkSelectOption[] ModeOptions { get; set; } = [];
    [Parameter] public FrameworkSelectOption[] NavModeOptions { get; set; } = [];

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnEventUpdated { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private string _editTitle = string.Empty;
    private string _editSubtitle = string.Empty;
    private string _editMode = "active";
    private string _editNavMode = "host_controlled";
    private bool _editIsHidden;
    private string _editScheduledTime = string.Empty;
    private bool _editAutoStart = true;
    private bool _isSavingEdit;
    private string? _editModalError;

    protected override void OnParametersSet()
    {
        if (IsOpen && EditingEvent is not null)
        {
            _editTitle = EditingEvent.Title;
            _editSubtitle = EditingEvent.Subtitle ?? string.Empty;
            _editMode = EditingEvent.Mode;
            _editNavMode = EditingEvent.NavigationMode;
            _editIsHidden = EditingEvent.IsHidden;
            _editScheduledTime = EditingEvent.ScheduledStartUtc.HasValue ? EditingEvent.ScheduledStartUtc.Value.ToLocalTime().ToString("HH:mm") : string.Empty;
            _editAutoStart = EditingEvent.AutoStartOnSchedule;
            _editModalError = null;
        }
    }

    private async Task SaveEditedEventAsync()
    {
        if (EditingEvent is null) return;
        _isSavingEdit = true;
        _editModalError = null;

        DateTime? schedUtc = ParseTimeInput(!string.IsNullOrWhiteSpace(_editScheduledTime), _editScheduledTime);

        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.UpdateEventAsync(
                        token,
                        ProjectId,
                        EditingEvent.EventId,
                        title: _editTitle,
                        subtitle: _editSubtitle,
                        navigationMode: _editNavMode,
                        scheduledStartUtc: schedUtc,
                        autoStartOnSchedule: _editAutoStart,
                        mode: _editMode,
                        isHidden: _editIsHidden);

                    await OnEventUpdated.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _editModalError = ex.Message;
        }
        finally
        {
            _isSavingEdit = false;
        }
    }

    private async Task CloseEditingEventAsync()
    {
        if (EditingEvent is null) return;
        var eventId = EditingEvent.EventId;
        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.CloseEventAsync(token, ProjectId, eventId);
                    await OnEventUpdated.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _editModalError = ex.Message;
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
