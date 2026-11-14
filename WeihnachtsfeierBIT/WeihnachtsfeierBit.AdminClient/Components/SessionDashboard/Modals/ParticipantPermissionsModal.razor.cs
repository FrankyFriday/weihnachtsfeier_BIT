using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class ParticipantPermissionsModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public ParticipantSummary? Participant { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnPermissionsSaved { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private Dictionary<string, bool> _permissionsValues = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSavingPermissions;
    private string? _permissionsModalError;

    protected override void OnParametersSet()
    {
        if (IsOpen && Participant is not null)
        {
            _permissionsValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ParticipantPermissionKeys.DefaultPermissions())
            {
                _permissionsValues[kvp.Key] = kvp.Value;
            }

            if (Participant.Permissions?.Values is not null)
            {
                foreach (var kvp in Participant.Permissions.Values)
                {
                    _permissionsValues[kvp.Key] = kvp.Value;
                }
            }

            _permissionsModalError = null;
        }
    }

    private void OnPermissionToggled(string key, bool value)
    {
        _permissionsValues[key] = value;
    }

    private async Task SaveParticipantPermissionsAsync()
    {
        if (Participant is null) return;
        _isSavingPermissions = true;
        _permissionsModalError = null;

        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.UpdateParticipantPermissionsAsync(token, ProjectId, Participant.Id, _permissionsValues);
                    await OnPermissionsSaved.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _permissionsModalError = ex.Message;
        }
        finally
        {
            _isSavingPermissions = false;
        }
    }
}
