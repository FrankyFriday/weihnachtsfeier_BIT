using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard;

public partial class ModerationSidebar : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public bool IsPinned { get; set; }
    [Parameter] public ParticipantSummary[] Participants { get; set; } = [];
    [Parameter] public TeamSummary[] Teams { get; set; } = [];
    [Parameter] public GameInstanceSummary? Instance { get; set; }

    [Parameter] public EventCallback OnToggleSidebar { get; set; }
    [Parameter] public EventCallback OnTogglePin { get; set; }
    [Parameter] public EventCallback<ParticipantSummary> OnOpenPermissions { get; set; }

    private string _regieActiveTab = "emulator";
    private string _regieSearchQuery = string.Empty;
    private string _regieFilter = "all";

    private ParticipantSummary? _selectedEmulatorParticipant;
    private long _emulatorTicks = DateTime.UtcNow.Ticks;

    protected override void OnParametersSet()
    {
        if (IsOpen && _selectedEmulatorParticipant is null && Participants.Length > 0)
        {
            _selectedEmulatorParticipant = Participants[0];
            _emulatorTicks = DateTime.UtcNow.Ticks;
        }
    }

    public void SelectAndEmulate(ParticipantSummary p)
    {
        _selectedEmulatorParticipant = p;
        _emulatorTicks = DateTime.UtcNow.Ticks;
        _regieActiveTab = "emulator";
    }

    private void OnEmulatorParticipantSelected(ChangeEventArgs e)
    {
        if (e.Value is string val && Guid.TryParse(val, out var parsedId))
        {
            var match = Participants.FirstOrDefault(p => p.Id == parsedId);
            if (match is not null)
            {
                _selectedEmulatorParticipant = match;
                _emulatorTicks = DateTime.UtcNow.Ticks;
            }
        }
    }

    private List<ParticipantSummary> FilteredParticipants
    {
        get
        {
            var list = Participants.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_regieSearchQuery))
            {
                var q = _regieSearchQuery.Trim().ToLowerInvariant();
                list = list.Where(p => p.Number.ToString().Contains(q) || (p.Name is not null && p.Name.ToLowerInvariant().Contains(q)));
            }
            if (_regieFilter == "muted")
            {
                list = list.Where(p => p.Permissions is not null && p.Permissions.HasPermission(ParticipantPermissionKeys.IsMuted) == false);
            }
            return list.OrderBy(p => p.Number).ToList();
        }
    }
}
