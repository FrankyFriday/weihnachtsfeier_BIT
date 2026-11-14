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

    private Dictionary<string, bool?> _permissionsValues = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, bool> _globalPermissions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSavingPermissions;
    private bool _isSavingGlobal;
    private string? _permissionsModalError;

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && RunAuthenticated is not null)
        {
            await RunAuthenticated(async token =>
            {
                var globalDict = await ProjectApiClient.GetProjectGlobalPermissionsAsync(token, ProjectId);
                _globalPermissions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in ParticipantPermissionKeys.DefaultPermissions())
                {
                    _globalPermissions[kvp.Key] = globalDict.TryGetValue(kvp.Key, out var val) && val.HasValue ? val.Value : kvp.Value;
                }
            });

            if (Participant is not null)
            {
                _permissionsValues = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
                if (Participant.PermissionOverrides is not null)
                {
                    foreach (var kvp in Participant.PermissionOverrides)
                    {
                        _permissionsValues[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
    }

    private void SetPermissionState(string key, bool? state)
    {
        _permissionsValues[key] = state;
    }

    private void OnGlobalPermissionToggled(string key, bool value)
    {
        _globalPermissions[key] = value;
    }

    private async Task SaveGlobalPermissionsAsync()
    {
        _isSavingGlobal = true;
        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    Dictionary<string, bool?> req = _globalPermissions.ToDictionary(k => k.Key, k => (bool?)k.Value, StringComparer.OrdinalIgnoreCase);
                    await ProjectApiClient.UpdateProjectGlobalPermissionsAsync(token, ProjectId, req);
                    await OnPermissionsSaved.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _permissionsModalError = ex.Message;
        }
        finally
        {
            _isSavingGlobal = false;
        }
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
