using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeihnachtsfeierBit.AdminClient.Components.SessionDashboard;
using WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Modals;
using WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Services;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components;

public partial class ProjectSessionDashboardContent : AuthenticatedComponentBase, IAsyncDisposable
{
    [Parameter] public Guid ProjectId { get; set; }

    [Inject] public IConfiguration Configuration { get; set; } = null!;
    [Inject] public ILogger<ProjectSessionDashboardContent> Logger { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private SessionDashboardState _state = null!;
    private ModerationSidebar? _moderationSidebar;

    private bool _isRegieSidebarOpen;
    private bool _isRegieSidebarPinned;

    private bool _isAwardPointsModalOpen;
    private Guid _awardDefaultTeamId;

    private bool _isPointLogModalOpen;
    private bool _isEditEventModalOpen;
    private EventState? _editingEvent;

    private bool _isLinkShowModalOpen;
    private bool _isStartSessionModalOpen;
    private bool _isSpontaneousModalOpen;
    private bool _isPermissionsModalOpen;
    private ParticipantSummary? _permissionsParticipant;

    private FrameworkDropdownOption[] _sessionOptions =>
    [
        new("share_project", "Projekt teilen / Admins", "person_add"),
        new("pause_session", _state?.SessionState?.IsPaused == true ? "Session fortsetzen" : "Session pausieren", _state?.SessionState?.IsPaused == true ? "play_arrow" : "pause"),
        new("end_session", "Session beenden", "power_settings_new")
    ];

    private static readonly FrameworkDropdownOption[] _addContentOptions =
    [
        new("show", "Show aus Katalog hinzufügen", "collections_bookmark"),
        new("spontaneous", "Spontanes Event erstellen", "add_circle")
    ];

    private static readonly FrameworkSelectOption[] _slideTypeOptions =
    [
        new("announcement", "Ankündigung"),
        new("poll", "Abstimmung"),
        new("quiz", "Quiz"),
        new("estimation", "Schätzfrage"),
        new("buzzer", "Buzzer"),
        new("ranking", "Rangliste"),
        new("text", "Freitext-Frage"),
        new("word_cloud", "Wortwolke"),
        new("slider", "Schieberegler"),
        new("rating", "Sterne-Bewertung"),
        new("ordering", "Reihenfolge sortieren"),
        new("countdown", "Timer / Countdown"),
        new("point_award", "Punktevergabe"),
        new("jeopardy", "Jeopardy"),
        new("pantomime", "Pantomime")
    ];

    private static readonly FrameworkSelectOption[] _modeOptions =
    [
        new("active", "Aktiv (Vordergrund-Takeover)"),
        new("passive", "Passiv (Menüliste)")
    ];

    private static readonly FrameworkSelectOption[] _navModeOptions =
    [
        new("host_controlled", "Moderiert (Vom Spielleiter gesteuert)"),
        new("self_paced", "Selbstständig (Teilnehmer blättern selbst)")
    ];

    protected override async Task OnInitializedAsync()
    {
        _state = new SessionDashboardState(ProjectId, ProjectApiClient, Configuration, Logger, async action => await RunAuthenticatedAsync(action));
        _state.OnStateChanged += StateHasChanged;
        await _state.InitializeAsync();
    }

    private string GetCanvasUrl() => Navigation.ToAbsoluteUri($"/play/beamer/{_state.Instance!.AccessCode}").ToString();

    private async Task HandleSessionOptionSelect(string optionValue)
    {
        switch (optionValue)
        {
            case "share_project":
                break;
            case "pause_session":
                await _state.TogglePauseAsync();
                break;
            case "end_session":
                await _state.EndSessionAsync();
                break;
        }
    }

    private void HandleAddContentSelect(string optionValue)
    {
        switch (optionValue)
        {
            case "show":
                _isLinkShowModalOpen = true;
                break;
            case "spontaneous":
                _isSpontaneousModalOpen = true;
                break;
        }
    }

    private void OpenAwardPointsForTeam(Guid teamId)
    {
        _awardDefaultTeamId = teamId;
        _isAwardPointsModalOpen = true;
    }

    private void CloseAwardPointsModal() => _isAwardPointsModalOpen = false;

    private void OpenPointLogModal() => _isPointLogModalOpen = true;
    private void ClosePointLogModal() => _isPointLogModalOpen = false;

    private void OpenEditEventModal(EventState ev)
    {
        _editingEvent = ev;
        _isEditEventModalOpen = true;
    }

    private void CloseEditEventModal() => _isEditEventModalOpen = false;
    private void CloseLinkShowModal() => _isLinkShowModalOpen = false;

    private void OpenStartSessionModal() => _isStartSessionModalOpen = true;
    private void CloseStartSessionModal() => _isStartSessionModalOpen = false;

    private void OnSessionStarted(GameInstanceSummary instance, string notice)
    {
        _state.SetInstance(instance, notice);
    }

    private void CloseSpontaneousModal() => _isSpontaneousModalOpen = false;

    private void OpenPermissionsModal(ParticipantSummary p)
    {
        _permissionsParticipant = p;
        _isPermissionsModalOpen = true;
    }

    private void ClosePermissionsModal() => _isPermissionsModalOpen = false;

    private void ToggleRegieSidebar() => _isRegieSidebarOpen = !_isRegieSidebarOpen;
    private void ToggleRegiePin() => _isRegieSidebarPinned = !_isRegieSidebarPinned;

    public async ValueTask DisposeAsync()
    {
        if (_state is not null)
        {
            _state.OnStateChanged -= StateHasChanged;
            await _state.DisposeAsync();
        }
    }
}
