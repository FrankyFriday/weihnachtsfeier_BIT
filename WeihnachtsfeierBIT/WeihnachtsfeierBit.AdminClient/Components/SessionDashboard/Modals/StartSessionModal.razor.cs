using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class StartSessionModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<(GameInstanceSummary Instance, string Notice)> OnSessionStarted { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private string _preferredCodeInput = string.Empty;
    private string? _startSessionError;
    private bool _isProcessing;

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            _preferredCodeInput = string.Empty;
            _startSessionError = null;
        }
    }

    private async Task ConfirmStartSessionAsync()
    {
        _isProcessing = true;
        _startSessionError = null;
        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    var instance = await ProjectApiClient.StartGameInstanceAsync(token, ProjectId, _preferredCodeInput);
                    var notice = $"Session mit Code {instance.AccessCode} erfolgreich gestartet!";
                    await OnSessionStarted.InvokeAsync((instance, notice));
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _startSessionError = ex.Message;
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
