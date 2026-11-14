using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeihnachtsfeierBit.AdminClient.Models;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Components.SessionDashboard.Services;

public class SessionDashboardState : IAsyncDisposable
{
    private readonly ProjectApiClient _apiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly Func<Func<string, Task>, Task> _runAuthenticated;

    private HubConnection? _hubConnection;
    private CancellationTokenSource? _connectionCancellation;
    private Task? _connectionRecoveryTask;

    public Guid ProjectId { get; private set; }
    public GameInstanceSummary? Instance { get; private set; }
    public TeamSummary[] Teams { get; private set; } = [];
    public ProjectShow[] ProjectShows { get; private set; } = [];
    public ParticipantSummary[] Participants { get; private set; } = [];

    public ProjectGameState? SessionState { get; private set; }
    public Guid? ActiveCanvasEventId { get; private set; }

    public List<TabItem> Tabs { get; } = [];
    public string? SelectedTabId { get; private set; }

    public bool IsLoading { get; private set; } = true;
    public string? ErrorMessage { get; private set; }
    public string? NoticeMessage { get; private set; }
    public string? ActionError { get; private set; }
    public bool IsProcessing { get; private set; }

    public HubConnection? HubConnection => _hubConnection;
    public TabItem? SelectedTabItem => Tabs.FirstOrDefault(t => t.Id == SelectedTabId);

    public event Action? OnStateChanged;

    public SessionDashboardState(
        Guid projectId,
        ProjectApiClient apiClient,
        IConfiguration configuration,
        ILogger logger,
        Func<Func<string, Task>, Task> runAuthenticated)
    {
        ProjectId = projectId;
        _apiClient = apiClient;
        _configuration = configuration;
        _logger = logger;
        _runAuthenticated = runAuthenticated;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        NotifyStateChanged();

        await _runAuthenticated(async token =>
        {
            await ConnectSignalRAsync(token);
            await LoadDataInternalAsync();
        });

        IsLoading = false;
        NotifyStateChanged();
    }

    private async Task ConnectSignalRAsync(string accessToken)
    {
        if (_hubConnection is not null) return;
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https+http://apiservice";
        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/gamestate";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken))
            .Build();

        _hubConnection.On<ProjectGameState>("GameStateUpdated", async state =>
        {
            if (state.ProjectId == ProjectId)
            {
                SessionState = state;
                ActiveCanvasEventId = state.ActiveCanvasEventId;
                await BuildTabsAsync();
                NotifyStateChanged();
            }
        });

        _connectionCancellation = new CancellationTokenSource();
        _connectionRecoveryTask = RecoverConnectionAsync(_hubConnection, accessToken, _connectionCancellation.Token);
    }

    private async Task RecoverConnectionAsync(HubConnection connection, string accessToken, CancellationToken cancellationToken)
    {
        await HubConnectionRecovery.RunAsync(
            connection,
            async token =>
            {
                await connection.SendAsync("JoinProject", ProjectId, token);
                var state = await _apiClient.GetSessionStateAsync(accessToken, ProjectId, token);
                SessionState = state;
                ActiveCanvasEventId = state.ActiveCanvasEventId;
                await BuildTabsAsync();
                NotifyStateChanged();
            },
            exception => _logger.LogWarning(exception, "Die Live-Verbindung des Session-Dashboards wird erneut aufgebaut."),
            _ => { NotifyStateChanged(); return Task.CompletedTask; },
            cancellationToken);
    }

    public async Task LoadDataAsync()
    {
        await _runAuthenticated(async token =>
        {
            await LoadDataInternalAsync();
        });
        NotifyStateChanged();
    }

    private async Task LoadDataInternalAsync()
    {
        await _runAuthenticated(async token =>
        {
            var instanceTask = _apiClient.GetActiveGameInstanceAsync(token, ProjectId);
            var teamsTask = _apiClient.GetTeamsAsync(token, ProjectId);
            var showsTask = _apiClient.GetShowsAsync(token, ProjectId);
            var participantsTask = _apiClient.GetParticipantsAsync(token, ProjectId);

            await Task.WhenAll(instanceTask, teamsTask, showsTask, participantsTask);

            Instance = await instanceTask;
            Teams = await teamsTask;
            ProjectShows = await showsTask;
            Participants = await participantsTask;

            if (Instance is not null)
            {
                SessionState = await _apiClient.GetSessionStateAsync(token, ProjectId);
                ActiveCanvasEventId = SessionState.ActiveCanvasEventId;
                await BuildTabsAsync();
            }
        });
    }

    private Task BuildTabsAsync()
    {
        Tabs.Clear();

        if (SessionState is null) return Task.CompletedTask;

        if (SessionState.ActiveEvent is EventState activeEv)
        {
            bool isBeamer = ActiveCanvasEventId.HasValue && activeEv.EventId == ActiveCanvasEventId.Value;
            Tabs.Add(new TabItem(activeEv.EventId.ToString(), activeEv.Title, "play_arrow", isBeamer, activeEv));
        }

        foreach (var passEv in SessionState.PassiveEvents)
        {
            bool isBeamer = ActiveCanvasEventId.HasValue && passEv.EventId == ActiveCanvasEventId.Value;
            Tabs.Add(new TabItem(passEv.EventId.ToString(), passEv.Title, null, isBeamer, passEv));
        }

        foreach (var schedEv in SessionState.ScheduledEvents)
        {
            bool isBeamer = ActiveCanvasEventId.HasValue && schedEv.EventId == ActiveCanvasEventId.Value;
            Tabs.Add(new TabItem(schedEv.EventId.ToString(), schedEv.Title, "schedule", isBeamer, schedEv));
        }

        if (SelectedTabId is null || !Tabs.Any(t => t.Id == SelectedTabId))
        {
            SelectedTabId = Tabs.FirstOrDefault()?.Id;
        }

        return Task.CompletedTask;
    }

    public void SelectTab(string tabId)
    {
        SelectedTabId = tabId;
        NotifyStateChanged();
    }

    public async Task TogglePauseAsync()
    {
        IsProcessing = true;
        ActionError = null;
        NotifyStateChanged();

        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.ToggleSessionPauseAsync(token, ProjectId);
                await LoadDataInternalAsync();
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        finally
        {
            IsProcessing = false;
            NotifyStateChanged();
        }
    }

    public async Task EndSessionAsync()
    {
        IsProcessing = true;
        ActionError = null;
        NotifyStateChanged();

        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.EndGameInstanceAsync(token, ProjectId);
                Instance = null;
                SessionState = null;
                Tabs.Clear();
                NoticeMessage = "Session wurde beendet.";
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        finally
        {
            IsProcessing = false;
            NotifyStateChanged();
        }
    }

    public async Task StepEventSlideToAsync(EventState ev, int targetSlideIndex)
    {
        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.UpdateEventAsync(token, ProjectId, ev.EventId, currentSlideIndex: targetSlideIndex);
                await LoadDataInternalAsync();
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        NotifyStateChanged();
    }

    public async Task StepSequenceNextAsync(EventState ev, EventNavigationContext navContext)
    {
        if (navContext.SequenceSlideIndices is null) return;
        var nextSeqIdx = navContext.CurrentSequenceIndex + 1;
        if (nextSeqIdx >= navContext.SequenceSlideIndices.Length)
        {
            await ReturnToJeopardyBoardAsync(ev);
            return;
        }

        var targetSlideIndex = navContext.SequenceSlideIndices[nextSeqIdx];
        await StepEventSlideToAsync(ev, targetSlideIndex);
    }

    public async Task ReturnToJeopardyBoardAsync(EventState ev)
    {
        if (ev.NavigationContext is not { } navContext) return;
        var originIndex = navContext.OriginSlideIndex;
        var activeCellKey = navContext.ActiveCellKey;

        var jeopardySlide = ev.Slides.Length > originIndex ? ev.Slides[originIndex] : null;
        if (jeopardySlide is not null)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (jeopardySlide.Parameters.ValueKind == JsonValueKind.Object
                && jeopardySlide.Parameters.TryGetProperty("state", out var stateElem)
                && stateElem.ValueKind == JsonValueKind.Object)
            {
                if (stateElem.TryGetProperty("used", out var usedElem) && usedElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var u in usedElem.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String))
                    {
                        used.Add(u.GetString()!);
                    }
                }
            }

            if (!string.IsNullOrEmpty(activeCellKey))
            {
                used.Add(activeCellKey);
            }

            var newStateObj = new
            {
                used = used.ToArray(),
                active = (string?)null,
                selectedTeamId = (string?)null,
                selectedTeamName = (string?)null,
                navigationContext = (object?)null
            };

            try
            {
                await _runAuthenticated(async token =>
                {
                    await _apiClient.SetSlideStateAsync(token, ProjectId, ev.EventId, jeopardySlide.SlideId, newStateObj);
                    await _apiClient.UpdateEventAsync(token, ProjectId, ev.EventId, currentSlideIndex: originIndex);
                    await LoadDataInternalAsync();
                });
            }
            catch (HttpRequestException ex)
            {
                ActionError = ex.Message;
            }
        }
        NotifyStateChanged();
    }

    public async Task CloseEventAsync(Guid eventId)
    {
        IsProcessing = true;
        ActionError = null;
        NotifyStateChanged();

        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.CloseEventAsync(token, ProjectId, eventId);
                await LoadDataInternalAsync();
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        finally
        {
            IsProcessing = false;
            NotifyStateChanged();
        }
    }

    public async Task SetCanvasEventAsync(Guid? eventId)
    {
        IsProcessing = true;
        ActionError = null;
        NotifyStateChanged();

        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.SetCanvasEventAsync(token, ProjectId, eventId);
                await LoadDataInternalAsync();
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        finally
        {
            IsProcessing = false;
            NotifyStateChanged();
        }
    }

    public async Task StartScheduledEventNowAsync(EventState ev)
    {
        IsProcessing = true;
        ActionError = null;
        NotifyStateChanged();

        try
        {
            await _runAuthenticated(async token =>
            {
                await _apiClient.UpdateEventAsync(token, ProjectId, ev.EventId, status: "active", mode: "active");
                await LoadDataInternalAsync();
            });
        }
        catch (HttpRequestException ex)
        {
            ActionError = ex.Message;
        }
        finally
        {
            IsProcessing = false;
            NotifyStateChanged();
        }
    }

    public void SetInstance(GameInstanceSummary instance, string noticeMessage)
    {
        Instance = instance;
        NoticeMessage = noticeMessage;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        _connectionCancellation?.Cancel();
        _connectionCancellation?.Dispose();

        if (_hubConnection is not null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch
            {
            }
        }
    }
}
