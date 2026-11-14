using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;

public partial class PointLogsModal : ComponentBase
{
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private TeamPointLogDto[] _pointLogs = [];

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && RunAuthenticated is not null)
        {
            await RunAuthenticated(async token =>
            {
                _pointLogs = await ProjectApiClient.GetPointLogsAsync(token, ProjectId);
            });
        }
    }
}
