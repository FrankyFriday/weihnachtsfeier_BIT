using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class AwardPointsModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public TeamSummary[] Teams { get; set; } = [];
    [Parameter] public Guid DefaultTeamId { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnPointsAwarded { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private Guid _awardTeamId;
    private int _awardDeltaPoints = 10;
    private string _awardReason = string.Empty;
    private bool _isSubmittingAward;
    private string? _awardModalError;

    private FrameworkSelectOption[] TeamSelectOptions =>
        Teams.Select(t => new FrameworkSelectOption(t.Id.ToString(), t.DisplayName)).ToArray();

    protected override void OnParametersSet()
    {
        if (IsOpen && _awardTeamId == Guid.Empty && Teams.Length > 0)
        {
            _awardTeamId = DefaultTeamId != Guid.Empty ? DefaultTeamId : Teams[0].Id;
            _awardDeltaPoints = 10;
            _awardReason = string.Empty;
            _awardModalError = null;
        }
    }

    private async Task SubmitAwardPointsAsync()
    {
        if (_awardTeamId == Guid.Empty)
        {
            _awardModalError = "Bitte wähle ein Team aus.";
            return;
        }

        _isSubmittingAward = true;
        _awardModalError = null;

        try
        {
            if (RunAuthenticated is not null)
            {
                await RunAuthenticated(async token =>
                {
                    await ProjectApiClient.AwardTeamPointsAsync(token, ProjectId, _awardTeamId, _awardDeltaPoints, _awardReason);
                    await OnPointsAwarded.InvokeAsync();
                    await OnClose.InvokeAsync();
                });
            }
        }
        catch (Exception ex)
        {
            _awardModalError = ex.Message;
        }
        finally
        {
            _isSubmittingAward = false;
        }
    }
}
