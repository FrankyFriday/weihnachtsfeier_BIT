using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard;

public partial class ModerationSidebar : ComponentBase
{
    private sealed record RegieCompartmentDefinition(string Id, string Title, string Icon);

    private static readonly RegieCompartmentDefinition[] Compartments =
    [
        new("emulator", "Handy-Emulator", "smartphone"),
        new("participants", "Teilnehmer & Rechte", "group"),
        new("leaderboard", "Punkte & Rangliste", "leaderboard"),
        new("chat", "Quick-Chat", "chat"),
        new("beamer", "Beamer-Steuerung", "present_to_all"),
        new("notes", "Regie-Notizen", "sticky_note_2"),
        new("responses", "Antworten", "forum")
    ];

    private const int MinimumCanvasPreviewWidth = 320;
    private const int MaximumCanvasPreviewWidth = 7680;
    private const int MinimumCanvasPreviewHeight = 240;
    private const int MaximumCanvasPreviewHeight = 4320;
    private const int DefaultCanvasPreviewWidth = 1920;
    private const int DefaultCanvasPreviewHeight = 1080;

    [Inject] public IJSRuntime JavaScript { get; set; } = null!;
    [Inject] public ILogger<ModerationSidebar> Logger { get; set; } = null!;
    [Inject] public ProjectApiClient ProjectApiClient { get; set; } = null!;

    [Parameter] public ParticipantSummary[] Participants { get; set; } = [];
    [Parameter] public TeamSummary[] Teams { get; set; } = [];
    [Parameter] public TeamPointLogDto[] PointLogs { get; set; } = [];
    [Parameter] public GameInstanceSummary? Instance { get; set; }
    [Parameter] public ProjectGameState? SessionState { get; set; }
    [Parameter] public EventState? SelectedEvent { get; set; }
    [Parameter] public Guid? ActiveCanvasEventId { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public string AccessToken { get; set; } = string.Empty;
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public bool CompactMode { get; set; }
    [Parameter] public string? CanvasUrl { get; set; }
    [Parameter] public CanvasDisplaySettings CanvasSettings { get; set; } = CanvasDisplaySettings.Default;
    [Parameter] public bool IsProcessing { get; set; }
    [Parameter] public string? ActionError { get; set; }

    [Parameter] public EventCallback<ParticipantSummary> OnOpenPermissions { get; set; }
    [Parameter] public EventCallback<bool> OnDrawerStateChanged { get; set; }
    [Parameter] public EventCallback<Guid> OnOpenAwardPoints { get; set; }
    [Parameter] public EventCallback<Guid?> OnSetCanvasEvent { get; set; }
    [Parameter] public EventCallback<CanvasDisplaySettings> OnSaveCanvasSettings { get; set; }
    [Parameter] public EventCallback OnEventCreated { get; set; }
    [Parameter] public Func<Func<string, Task>, Task>? RunAuthenticated { get; set; }

    private bool _isDrawerOpen;
    private string _activeCompartment = "emulator";

    private string _regieSearchQuery = string.Empty;
    private string _regieFilter = "all";

    private ParticipantSummary? _selectedEmulatorParticipant;
    private long _emulatorTicks = DateTime.UtcNow.Ticks;

    private bool _isZwischenstandDropdownOpen;
    private bool _isCanvasSettingsSaving;
    private string _draftCanvasEventId = string.Empty;
    private CanvasDisplaySettings _draftCanvasSettings = CanvasDisplaySettings.Default;
    private CanvasDisplaySettings? _lastCanvasSettings;
    private Guid? _lastActiveCanvasEventId;
    private bool _isCanvasPreviewResolutionModalOpen;
    private int _canvasPreviewWidth = DefaultCanvasPreviewWidth;
    private int _canvasPreviewHeight = DefaultCanvasPreviewHeight;
    private int _draftCanvasPreviewWidth = DefaultCanvasPreviewWidth;
    private int _draftCanvasPreviewHeight = DefaultCanvasPreviewHeight;
    private string? _canvasPreviewResolutionError;
    private ElementReference _canvasPreviewStage;
    private ElementReference _canvasPreviewFrame;
    private ElementReference _drawerResizeHandle;
    private IJSObjectReference? _drawerResizeModule;
    private IJSObjectReference? _drawerResizeController;
    private IJSObjectReference? _canvasPreviewModule;
    private IJSObjectReference? _canvasPreviewObserver;
    private int _observedCanvasPreviewWidth;
    private int _observedCanvasPreviewHeight;

    private string CanvasPreviewResolutionLabel => $"{_canvasPreviewWidth} × {_canvasPreviewHeight} px";
    private EventSlideState? CurrentSlide => SelectedEvent is { } selectedEvent
        && selectedEvent.CurrentSlideIndex >= 0
        && selectedEvent.CurrentSlideIndex < selectedEvent.Slides.Length
            ? selectedEvent.Slides[selectedEvent.CurrentSlideIndex]
            : null;
    private string CanvasPreviewStageStyle
    {
        get
        {
            var aspectRatio = _canvasPreviewWidth / (double)_canvasPreviewHeight;
            var maxWidth = 220d * aspectRatio;
            return FormattableString.Invariant(
                $"--canvas-preview-aspect-ratio:{aspectRatio:0.####};--canvas-preview-max-width:{maxWidth:0.##}px");
        }
    }

    private RegieCompartmentDefinition SelectedCompartment =>
        Compartments.FirstOrDefault(compartment => compartment.Id == _activeCompartment) ?? Compartments[0];
    private EventState[] CanvasEvents
    {
        get
        {
            if (SessionState is null)
            {
                return [];
            }

            return SessionState.ActiveEvent is { } activeEvent
                ? [activeEvent, .. SessionState.PassiveEvents]
                : SessionState.PassiveEvents;
        }
    }
    private FrameworkSelectOption[] CanvasEventOptions =>
        [
            new(string.Empty, "Kein Event · nur Kopfbereich und Punktestand"),
            .. CanvasEvents.Select(gameEvent => new FrameworkSelectOption(
                gameEvent.EventId.ToString(),
                gameEvent.Title))
        ];
    private static FrameworkSelectOption[] ScoreboardLayoutOptions =>
    [
        new(CanvasDisplaySettings.ScoreboardRight, "Rechts · Punktestand und Diagramm"),
        new(CanvasDisplaySettings.ScoreboardBottom, "Unten · schmale Punkte-Leiste"),
        new(CanvasDisplaySettings.ScoreboardHidden, "Nicht anzeigen")
    ];
    private static FrameworkSelectOption[] QrCodeModeOptions =>
    [
        new(CanvasDisplaySettings.QrCodeCard, "Kleine Karte im Kopfbereich"),
        new(CanvasDisplaySettings.QrCodePopup, "Großes Popup mit Session-Code"),
        new(CanvasDisplaySettings.QrCodeHidden, "Ausblenden")
    ];

    protected override void OnParametersSet()
    {
        if (_selectedEmulatorParticipant is null && Participants is not null && Participants.Length > 0)
        {
            _selectedEmulatorParticipant = Participants[0];
            _emulatorTicks = DateTime.UtcNow.Ticks;
        }

        if (_lastActiveCanvasEventId != ActiveCanvasEventId)
        {
            _lastActiveCanvasEventId = ActiveCanvasEventId;
            _draftCanvasEventId = ActiveCanvasEventId?.ToString() ?? string.Empty;
        }

        if (!Equals(_lastCanvasSettings, CanvasSettings))
        {
            _lastCanvasSettings = CanvasSettings;
            _draftCanvasSettings = CanvasSettings;
        }
    }

    private Task SelectCompartment(string compartment)
    {
        _activeCompartment = compartment;
        _isDrawerOpen = true;
        return CompactMode ? Task.CompletedTask : OnDrawerStateChanged.InvokeAsync(true);
    }

    public async Task ToggleCompartment(string compartment)
    {
        if (_isDrawerOpen && _activeCompartment == compartment)
        {
            _isDrawerOpen = false;
        }
        else
        {
            _activeCompartment = compartment;
            _isDrawerOpen = true;
        }
        await OnDrawerStateChanged.InvokeAsync(_isDrawerOpen);
    }

    public async Task CloseDrawer()
    {
        _isDrawerOpen = false;
        await OnDrawerStateChanged.InvokeAsync(false);
    }

    public async Task SelectAndEmulate(ParticipantSummary p)
    {
        _selectedEmulatorParticipant = p;
        _emulatorTicks = DateTime.UtcNow.Ticks;
        _activeCompartment = "emulator";
        _isDrawerOpen = true;
        await OnDrawerStateChanged.InvokeAsync(true);
    }

    private void ToggleZwischenstandDropdown() => _isZwischenstandDropdownOpen = !_isZwischenstandDropdownOpen;

    private async Task StartZwischenstandEventAsync(string mode)
    {
        _isZwischenstandDropdownOpen = false;
        var parameters = JsonSerializer.SerializeToElement(new
        {
            internalName = "Zwischenstand (Verlauf)",
            title = "Zwischenstand",
            chartType = "line_graph",
            animatePodium = false
        });

        if (RunAuthenticated is not null)
        {
            await RunAuthenticated(async token =>
            {
                await ProjectApiClient.CreateSpontaneousEventAsync(
                    token,
                    ProjectId,
                    "ranking",
                    mode,
                    parameters,
                    title: "Zwischenstand",
                    subtitle: "Aktueller Punktestand",
                    navigationMode: "host_controlled",
                    isHidden: false,
                    scheduledStartUtc: null,
                    autoStartOnSchedule: true);

                await OnEventCreated.InvokeAsync();
            });
        }
    }

    private FrameworkSelectOption[] ParticipantSelectOptions => Participants?
        .OrderBy(p => p.Number)
        .Select(p => new FrameworkSelectOption(p.Id.ToString(), $"#{p.Number} {(string.IsNullOrWhiteSpace(p.Name) ? "" : $"({p.Name})")}".Trim()))
        .ToArray() ?? [];

    private void OpenCanvasPreviewResolutionModal()
    {
        _draftCanvasPreviewWidth = _canvasPreviewWidth;
        _draftCanvasPreviewHeight = _canvasPreviewHeight;
        _canvasPreviewResolutionError = null;
        _isCanvasPreviewResolutionModalOpen = true;
    }

    private void CloseCanvasPreviewResolutionModal() =>
        _isCanvasPreviewResolutionModalOpen = false;

    private Task ApplyCanvasPreviewResolutionAsync()
    {
        if (_draftCanvasPreviewWidth is < MinimumCanvasPreviewWidth or > MaximumCanvasPreviewWidth ||
            _draftCanvasPreviewHeight is < MinimumCanvasPreviewHeight or > MaximumCanvasPreviewHeight)
        {
            _canvasPreviewResolutionError =
                $"Breite muss zwischen {MinimumCanvasPreviewWidth} und {MaximumCanvasPreviewWidth} und Höhe zwischen {MinimumCanvasPreviewHeight} und {MaximumCanvasPreviewHeight} Pixeln liegen.";
            return Task.CompletedTask;
        }

        _canvasPreviewWidth = _draftCanvasPreviewWidth;
        _canvasPreviewHeight = _draftCanvasPreviewHeight;
        _canvasPreviewResolutionError = null;
        _isCanvasPreviewResolutionModalOpen = false;
        return Task.CompletedTask;
    }

    private void OnEmulatorParticipantSelectChanged(string? val)
    {
        if (val is not null && Guid.TryParse(val, out var parsedId))
        {
            var match = Participants?.FirstOrDefault(p => p.Id == parsedId);
            if (match is not null)
            {
                _selectedEmulatorParticipant = match;
                _emulatorTicks = DateTime.UtcNow.Ticks;
            }
        }
    }

    private void SetRegieFilter(string filter)
    {
        _regieFilter = filter;
    }

    private async Task OnCanvasEventSelectedAsync(string? value)
    {
        Guid? eventId = null;
        if (!string.IsNullOrEmpty(value))
        {
            if (!Guid.TryParse(value, out var parsedEventId) ||
                CanvasEvents.All(gameEvent => gameEvent.EventId != parsedEventId))
            {
                return;
            }

            eventId = parsedEventId;
        }

        _draftCanvasEventId = eventId?.ToString() ?? string.Empty;
        try
        {
            await OnSetCanvasEvent.InvokeAsync(eventId);
        }
        finally
        {
            _draftCanvasEventId = ActiveCanvasEventId?.ToString() ?? string.Empty;
        }
    }

    private Task OnScoreboardLayoutSelectedAsync(string? value) =>
        value is CanvasDisplaySettings.ScoreboardRight or
            CanvasDisplaySettings.ScoreboardBottom or
            CanvasDisplaySettings.ScoreboardHidden
            ? SaveCanvasSettingsAsync(_draftCanvasSettings with { ScoreboardLayout = value })
            : Task.CompletedTask;

    private Task OnQrCodeModeSelectedAsync(string? value) =>
        value is CanvasDisplaySettings.QrCodeCard or
            CanvasDisplaySettings.QrCodePopup or
            CanvasDisplaySettings.QrCodeHidden
            ? SaveCanvasSettingsAsync(_draftCanvasSettings with { QrCodeMode = value })
            : Task.CompletedTask;

    private Task OnShowProjectTitleChangedAsync(bool value) =>
        SaveCanvasSettingsAsync(_draftCanvasSettings with { ShowProjectTitle = value });

    private Task OnShowEventTypeLabelChangedAsync(bool value) =>
        SaveCanvasSettingsAsync(_draftCanvasSettings with { ShowEventTypeLabel = value });

    private Task OnShowActiveTeamChangedAsync(bool value) =>
        SaveCanvasSettingsAsync(_draftCanvasSettings with { ShowActiveTeam = value });

    private Task AdjustFontScaleAsync(int delta) =>
        AdjustCanvasScaleAsync(value => _draftCanvasSettings with { FontScalePercent = value },
            _draftCanvasSettings.FontScalePercent, delta);

    private Task AdjustSpacingScaleAsync(int delta) =>
        AdjustCanvasScaleAsync(value => _draftCanvasSettings with { SpacingScalePercent = value },
            _draftCanvasSettings.SpacingScalePercent, delta);

    private Task AdjustEventScaleAsync(int delta) =>
        AdjustCanvasScaleAsync(value => _draftCanvasSettings with { EventScalePercent = value },
            _draftCanvasSettings.EventScalePercent, delta);

    private Task AdjustScoreboardScaleAsync(int delta) =>
        AdjustCanvasScaleAsync(value => _draftCanvasSettings with { ScoreboardScalePercent = value },
            _draftCanvasSettings.ScoreboardScalePercent, delta);

    private Task AdjustQrCodeScaleAsync(int delta) =>
        AdjustCanvasScaleAsync(value => _draftCanvasSettings with { QrCodeScalePercent = value },
            _draftCanvasSettings.QrCodeScalePercent, delta);

    private Task AdjustCanvasScaleAsync(
        Func<int, CanvasDisplaySettings> update,
        int currentScale,
        int delta)
    {
        var nextScale = Math.Clamp(
            currentScale + delta,
            CanvasDisplaySettings.MinimumScalePercent,
            CanvasDisplaySettings.MaximumScalePercent);
        return nextScale == currentScale
            ? Task.CompletedTask
            : SaveCanvasSettingsAsync(update(nextScale));
    }

    private async Task SaveCanvasSettingsAsync(CanvasDisplaySettings settings)
    {
        _draftCanvasSettings = settings;
        _isCanvasSettingsSaving = true;
        try
        {
            await OnSaveCanvasSettings.InvokeAsync(settings);
        }
        finally
        {
            _draftCanvasSettings = CanvasSettings;
            _isCanvasSettingsSaving = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            _drawerResizeModule ??= await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", "./js/drawer-resize.js");
            _drawerResizeController ??= await _drawerResizeModule.InvokeAsync<IJSObjectReference>(
                "attachDrawerResize",
                _drawerResizeHandle);
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Die Breite der Regie-Schublade konnte nach dem Trennen der UI-Verbindung nicht angepasst werden.");
        }

        if (_activeCompartment != "beamer" || Instance is null || string.IsNullOrWhiteSpace(CanvasUrl))
        {
            _canvasPreviewObserver = await DisposeCanvasPreviewObserverAsync(_canvasPreviewObserver);
            _observedCanvasPreviewWidth = 0;
            _observedCanvasPreviewHeight = 0;
            return;
        }

        try
        {
            _canvasPreviewModule ??= await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", "./js/canvas-preview.js");

            if (_canvasPreviewObserver is not null &&
                _observedCanvasPreviewWidth == _canvasPreviewWidth &&
                _observedCanvasPreviewHeight == _canvasPreviewHeight)
            {
                await _canvasPreviewObserver.InvokeVoidAsync("refresh");
                return;
            }

            _canvasPreviewObserver = await DisposeCanvasPreviewObserverAsync(_canvasPreviewObserver);
            _canvasPreviewObserver = await _canvasPreviewModule.InvokeAsync<IJSObjectReference>(
                "observeCanvasPreview",
                _canvasPreviewStage,
                _canvasPreviewFrame,
                _canvasPreviewWidth,
                _canvasPreviewHeight);
            _observedCanvasPreviewWidth = _canvasPreviewWidth;
            _observedCanvasPreviewHeight = _canvasPreviewHeight;
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Die Beamer-Vorschau konnte nach dem Trennen der UI-Verbindung nicht angepasst werden.");
        }
    }

    private async Task<IJSObjectReference?> DisposeCanvasPreviewObserverAsync(IJSObjectReference? observer)
    {
        if (observer is null)
        {
            return null;
        }

        try
        {
            await observer.InvokeVoidAsync("dispose");
            await observer.DisposeAsync();
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Die Beamer-Vorschau konnte nach dem Trennen der UI-Verbindung nicht bereinigt werden.");
        }

        return null;
    }

    private int TotalFilteredCount
    {
        get
        {
            var list = Participants?.AsEnumerable() ?? Enumerable.Empty<ParticipantSummary>();
            if (!string.IsNullOrWhiteSpace(_regieSearchQuery))
            {
                var q = _regieSearchQuery.Trim().ToLowerInvariant();
                list = list.Where(p => p.Number.ToString().Contains(q) || (p.Name is not null && p.Name.ToLowerInvariant().Contains(q)));
            }
            return list.Count();
        }
    }

    private List<ParticipantSummary> FilteredParticipants
    {
        get
        {
            var list = Participants?.AsEnumerable() ?? Enumerable.Empty<ParticipantSummary>();
            if (!string.IsNullOrWhiteSpace(_regieSearchQuery))
            {
                var q = _regieSearchQuery.Trim().ToLowerInvariant();
                list = list.Where(p => p.Number.ToString().Contains(q) || (p.Name is not null && p.Name.ToLowerInvariant().Contains(q)));
            }
            if (_regieFilter == "modified")
            {
                list = list.Where(p => p.PermissionOverrides is not null && p.PermissionOverrides.Values.Any(v => v == true || v == false));
            }
            else if (_regieFilter == "plus")
            {
                list = list.Where(p => p.PermissionOverrides is not null && p.PermissionOverrides.Values.Any(v => v == true));
            }
            else if (_regieFilter == "minus")
            {
                list = list.Where(p => p.PermissionOverrides is not null && p.PermissionOverrides.Values.Any(v => v == false));
            }
            else if (_regieFilter == "muted")
            {
                list = list.Where(p =>
                    p.Permissions?.HasPermission(ParticipantPermissionKeys.IsMuted) == false);
            }
            return list.OrderBy(p => p.Number).ToList();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _canvasPreviewObserver = await DisposeCanvasPreviewObserverAsync(_canvasPreviewObserver);
        if (_drawerResizeController is not null)
        {
            try
            {
                await _drawerResizeController.InvokeVoidAsync("dispose");
                await _drawerResizeController.DisposeAsync();
            }
            catch (JSDisconnectedException exception)
            {
                Logger.LogDebug(exception, "Die Regie-Schublade konnte nach dem Trennen der UI-Verbindung nicht freigegeben werden.");
            }
        }
        if (_drawerResizeModule is not null)
        {
            try
            {
                await _drawerResizeModule.DisposeAsync();
            }
            catch (JSDisconnectedException exception)
            {
                Logger.LogDebug(exception, "Das Regie-Schubladen-Modul konnte nach dem Trennen der UI-Verbindung nicht freigegeben werden.");
            }
        }
        if (_canvasPreviewModule is not null)
        {
            try
            {
                await _canvasPreviewModule.DisposeAsync();
            }
            catch (JSDisconnectedException exception)
            {
                Logger.LogDebug(exception, "Das Beamer-Vorschau-Modul konnte nach dem Trennen der UI-Verbindung nicht freigegeben werden.");
            }
        }
    }
}
