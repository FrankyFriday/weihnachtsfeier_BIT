using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;
using PantomimeAdminData = WeihnachtsfeierBit.Framework.PantomimeAdminData;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class SessionControlEndpoints
{
    private static readonly ConcurrentDictionary<(Guid EventId, Guid SlideId), SemaphoreSlim> PantomimeLocks = new();

    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/session/state", GetSessionStateAsync).WithName("GetSessionState");
        HitsterEndpoints.Map(reader, admin);
        reader.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/responses", SubmitEventResponseAsync).WithName("SubmitEventResponse");

        admin.MapPost("/session/shows", StartShowInSessionAsync).WithName("StartShowInSession");
        admin.MapPut("/session/events/{eventId:guid}", UpdateSessionEventAsync).WithName("UpdateSessionEvent");
        admin.MapPut("/session/events/{eventId:guid}/slides/{slideId:guid}/state", SetSlideStateAsync).WithName("SetSlideState");
        admin.MapGet("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime", GetPantomimeAsync).WithName("GetPantomime");
        admin.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/{action}", UpdatePantomimeAsync).WithName("UpdatePantomime");
        admin.MapPut("/session/events/{eventId:guid}/mode", UpdateEventModeAsync).WithName("UpdateEventMode");
        admin.MapPut("/session/events/{eventId:guid}/close", CloseEventAsync).WithName("CloseEvent");
        admin.MapPut("/session/canvas", SetCanvasEventAsync).WithName("SetCanvasEvent");
        admin.MapPost("/session/events", CreateSpontaneousEventAsync).WithName("CreateSpontaneousEvent");
        admin.MapPost("/session/toggle-pause", ToggleSessionPauseAsync).WithName("ToggleSessionPause");
    }

    public sealed record PantomimeActionRequest(Guid? TeamId, Guid? PerformerId);

    private static async Task<IResult> GetPantomimeAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.Forbid();
        }

        var gameEvent = await dbContext.GameInstanceEvents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }

        var slide = FindPantomimeSlide(gameEvent.SlidesJson, slideId);
        if (slide is null)
        {
            return Results.NotFound();
        }

        var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
        var terms = parameters["terms"]?.AsArray()
            .Select(term => term?.GetValue<string>() ?? string.Empty)
            .ToArray() ?? [];
        return Results.Ok(new PantomimeAdminData(
            parameters["category"]?.GetValue<string>() ?? string.Empty,
            terms,
            parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60,
            JsonSerializer.SerializeToElement(parameters["state"] ?? new JsonObject())));
    }

    private static async Task<IResult> UpdatePantomimeAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        string action,
        PantomimeActionRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.Forbid();
        }

        if (action is not ("start" or "correct" or "pass" or "pause" or "resume" or "end"))
        {
            return Results.BadRequest(new ApiError("Unbekannte Pantomime-Aktion."));
        }

        var gameLock = PantomimeLocks.GetOrAdd((eventId, slideId), _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var gameEvent = await dbContext.GameInstanceEvents
                .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
            if (gameEvent is null)
            {
                return Results.NotFound();
            }

            var slides = JsonNode.Parse(gameEvent.SlidesJson) as JsonArray;
            var slide = slides is null ? null : FindPantomimeSlide(slides, slideId);
            if (slide is null)
            {
                return Results.NotFound();
            }

            var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
            var terms = parameters["terms"]?.AsArray()
                .Select(term => term?.GetValue<string>() ?? string.Empty)
                .ToArray() ?? [];
            if (terms.Length == 0)
            {
                return Results.BadRequest(new ApiError("Für diese Pantomime-Slide sind keine Begriffe hinterlegt."));
            }

            var state = parameters["state"] as JsonObject ?? new JsonObject();
            var now = DateTime.UtcNow;
            var isRunning = state["isRunning"]?.GetValue<bool>() ?? false;
            var isPaused = state["isPaused"]?.GetValue<bool>() ?? false;
            var termIndex = state["termIndex"]?.GetValue<int>() ?? 0;

            switch (action)
            {
                case "start":
                    if (isRunning || isPaused || request.TeamId is not Guid teamId || request.PerformerId is not Guid performerId)
                    {
                        return Results.Conflict(new ApiError("Wähle ein Team und eine darstellende Person für die neue Runde."));
                    }

                    var team = await dbContext.Teams.FirstOrDefaultAsync(
                        item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
                    var performer = await dbContext.Teilnehmer.FirstOrDefaultAsync(
                        item => item.Id == performerId && item.ProjektId == projectId && item.TeamId == teamId, cancellationToken);
                    if (team is null || performer is null)
                    {
                        return Results.BadRequest(new ApiError("Die darstellende Person muss zum ausgewählten Projekt-Team gehören."));
                    }

                    var completedTeamIds = state["completedTeamIds"] as JsonArray;
                    if (completedTeamIds is null)
                    {
                        completedTeamIds = new JsonArray();
                        state["completedTeamIds"] = completedTeamIds;
                    }
                    if (completedTeamIds.Any(item => Guid.TryParse(item?.GetValue<string>(), out var completedTeamId) && completedTeamId == teamId))
                    {
                        return Results.Conflict(new ApiError("Dieses Team hat seine Pantomime-Runde bereits gespielt."));
                    }

                    state["termIndex"] = 0;
                    state["teamId"] = team.Id;
                    state["teamName"] = team.DisplayName;
                    state["performerId"] = performer.Id;
                    state["performerName"] = string.IsNullOrWhiteSpace(performer.Name) ? $"Person {performer.Nummer}" : performer.Name;
                    state["correctCount"] = 0;
                    state["isPaused"] = false;
                    state["isRunning"] = true;
                    state["deadlineUtc"] = now.AddSeconds(parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60);
                    state["remainingSeconds"] = parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60;
                    break;
                case "correct":
                case "pass":
                    if (!isRunning || termIndex >= terms.Length ||
                        state["deadlineUtc"]?.GetValue<DateTime>() is not DateTime deadline || deadline <= now)
                    {
                        return Results.Conflict(new ApiError("Die Pantomime-Runde läuft nicht oder ist abgelaufen."));
                    }
                    if (action == "correct")
                    {
                        if (state["teamId"]?.GetValue<Guid>() is not Guid scoringTeamId)
                        {
                            return Results.Conflict(new ApiError("Für die Runde ist kein Team ausgewählt."));
                        }

                        var scoringTeam = await dbContext.Teams.FirstOrDefaultAsync(
                            item => item.Id == scoringTeamId && item.ProjektId == projectId, cancellationToken);
                        if (scoringTeam is null)
                        {
                            return Results.Conflict(new ApiError("Das Team der aktuellen Runde existiert nicht mehr."));
                        }

                        scoringTeam.Punkte = checked(scoringTeam.Punkte + 1);
                        dbContext.TeamPointLogs.Add(new TeamPointLog
                        {
                            ProjektId = projectId,
                            TeamId = scoringTeam.Id,
                            DeltaPoints = 1,
                            NewTotalPoints = scoringTeam.Punkte,
                            Reason = "Pantomime-Begriff erraten",
                            CreatedAtUtc = now
                        });
                        state["correctCount"] = (state["correctCount"]?.GetValue<int>() ?? 0) + 1;
                    }

                    termIndex++;
                    state["termIndex"] = termIndex;
                    if (termIndex >= terms.Length)
                    {
                        state["isRunning"] = false;
                        state["isPaused"] = false;
                        state["remainingSeconds"] = 0;
                        MarkPantomimeTeamCompleted(state);
                    }
                    break;
                case "pause":
                    if (!isRunning || state["deadlineUtc"]?.GetValue<DateTime>() is not DateTime pauseDeadline)
                    {
                        return Results.Conflict(new ApiError("Es läuft keine Pantomime-Runde."));
                    }
                    state["remainingSeconds"] = Math.Max(0, (int)Math.Ceiling((pauseDeadline - now).TotalSeconds));
                    if ((int)state["remainingSeconds"]! == 0)
                    {
                        return Results.Conflict(new ApiError("Die Pantomime-Runde ist abgelaufen."));
                    }
                    state["isRunning"] = false;
                    state["isPaused"] = true;
                    break;
                case "resume":
                    if (!isPaused)
                    {
                        return Results.Conflict(new ApiError("Die Pantomime-Runde ist nicht pausiert."));
                    }
                    var remainingSeconds = Math.Max(0, state["remainingSeconds"]?.GetValue<int>() ?? 0);
                    if (remainingSeconds == 0)
                    {
                        state["isPaused"] = false;
                        MarkPantomimeTeamCompleted(state);
                    }
                    else
                    {
                        state["deadlineUtc"] = now.AddSeconds(remainingSeconds);
                        state["isPaused"] = false;
                        state["isRunning"] = true;
                    }
                    break;
                case "end":
                    if (isRunning || isPaused)
                    {
                        MarkPantomimeTeamCompleted(state);
                    }
                    state["isRunning"] = false;
                    state["isPaused"] = false;
                    state["remainingSeconds"] = 0;
                    break;
            }

            parameters["state"] = state;
            slide["parameters"] = parameters;
            gameEvent.SlidesJson = slides!.ToJsonString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
            return Results.Ok();
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static JsonObject? FindPantomimeSlide(string slidesJson, Guid slideId) =>
        JsonNode.Parse(slidesJson) is JsonArray slides ? FindPantomimeSlide(slides, slideId) : null;

    private static JsonObject? FindPantomimeSlide(JsonArray slides, Guid slideId)
    {
        var slide = PantomimeGameService.FindSlide(slides, slideId);
        return string.Equals(slide?["type"]?.GetValue<string>(), "pantomime", StringComparison.OrdinalIgnoreCase)
            ? slide
            : null;
    }

    private static void MarkPantomimeTeamCompleted(JsonObject state)
    {
        if (state["teamId"]?.GetValue<Guid>() is not Guid teamId)
        {
            return;
        }

        var completedTeamIds = state["completedTeamIds"] as JsonArray;
        if (completedTeamIds is null)
        {
            completedTeamIds = new JsonArray();
            state["completedTeamIds"] = completedTeamIds;
        }
        if (!completedTeamIds.Any(item => Guid.TryParse(item?.GetValue<string>(), out var completedTeamId) && completedTeamId == teamId))
        {
            completedTeamIds.Add(teamId.ToString());
        }
    }

    private static bool IsValidPantomimeConfiguration(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("category", out var category)
            || category.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(category.GetString())
            || category.GetString()!.Length > 120
            || !parameters.TryGetProperty("terms", out var terms)
            || terms.ValueKind != JsonValueKind.Array
            || !parameters.TryGetProperty("roundDurationSeconds", out var duration)
            || duration.ValueKind != JsonValueKind.Number
            || !duration.TryGetInt32(out var seconds)
            || seconds is < 10 or > 600)
        {
            return false;
        }

        var cleanTerms = terms.EnumerateArray()
            .Where(term => term.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(term.GetString()))
            .Select(term => term.GetString()!.Trim())
            .ToArray();
        return cleanTerms.Length is >= 3 and <= 100 && cleanTerms.All(term => term.Length <= 120);
    }


    private static async Task<IResult> ToggleSessionPauseAsync(
        Guid projectId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Session zu pausieren."));
        }

        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Keine aktive Session für dieses Projekt gefunden."));
        }

        instance.IsPaused = !instance.IsPaused;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new { isPaused = instance.IsPaused });
    }

    private static async Task<IResult> GetSessionStateAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var state = await GameStateNotifier.BuildProjectGameStateAsync(projectId, dbContext, cancellationToken);
        if (state is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(state);
    }

    private static async Task<IResult> StartShowInSessionAsync(
        Guid projectId,
        StartSessionShowRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Live-Session zu steuern."));
        }

        var instance = await dbContext.GameInstances
            .Include(gi => gi.Events)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden. Starte zuerst eine Session."));
        }

        var show = await dbContext.Shows
            .Include(s => s.Slides)
            .FirstOrDefaultAsync(s => s.Id == request.ShowId, cancellationToken);

        if (show is null)
        {
            return Results.NotFound(new ApiError("Show nicht gefunden."));
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";
        string navMode = string.Equals(request.NavigationMode, "self_paced", StringComparison.OrdinalIgnoreCase) ? "self_paced" : "host_controlled";
        bool isHidden = request.IsHidden ?? false;
        DateTime? schedUtc = request.ScheduledStartUtc;
        bool autoStart = request.AutoStartOnSchedule ?? false;
        string status = (schedUtc.HasValue && schedUtc.Value > DateTime.UtcNow) ? "scheduled" : "active";

        if (mode == "active" && status == "active")
        {
            MakeAllEventsPassive(instance);
        }

        string eventTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title.Trim() : show.Name;

        var slidesPayload = show.Slides.OrderBy(s => s.Position).Select(s => new
        {
            slideId = s.Id,
            type = GetSlideType(s),
            position = s.Position,
            parameters = JsonDocument.Parse(s.ParametersJson).RootElement
        }).ToList();

        var showEvent = new GameInstanceEvent
        {
            GameInstanceId = instance.Id,
            SourceShowId = show.Id,
            Title = eventTitle,
            Subtitle = request.Subtitle,
            Mode = mode,
            Status = status,
            NavigationMode = navMode,
            CurrentSlideIndex = 0,
            IsHidden = isHidden,
            ScheduledStartUtc = schedUtc,
            AutoStartOnSchedule = autoStart,
            SlidesJson = JsonSerializer.Serialize(slidesPayload)
        };

        dbContext.GameInstanceEvents.Add(showEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new { eventId = showEvent.Id });
    }

    private static async Task<IResult> UpdateSessionEventAsync(
        Guid projectId,
        Guid eventId,
        UpdateSessionEventRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Live-Session zu steuern."));
        }

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            ev.Title = request.Title.Trim();
        }

        if (request.Subtitle is not null)
        {
            ev.Subtitle = string.IsNullOrWhiteSpace(request.Subtitle) ? null : request.Subtitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NavigationMode))
        {
            ev.NavigationMode = string.Equals(request.NavigationMode, "self_paced", StringComparison.OrdinalIgnoreCase) ? "self_paced" : "host_controlled";
        }

        if (request.ScheduledStartUtc.HasValue)
        {
            ev.ScheduledStartUtc = request.ScheduledStartUtc.Value;
        }

        if (request.AutoStartOnSchedule.HasValue)
        {
            ev.AutoStartOnSchedule = request.AutoStartOnSchedule.Value;
        }

        if (request.StartedAtUtc.HasValue)
        {
            ev.StartedAtUtc = request.StartedAtUtc.Value;
        }

        if (request.AccumulatedElapsedSeconds.HasValue)
        {
            ev.AccumulatedElapsedSeconds = Math.Max(0, request.AccumulatedElapsedSeconds.Value);
        }

        if (request.IsPaused.HasValue)
        {
            bool wantsPause = request.IsPaused.Value;
            if (wantsPause && !ev.IsPaused)
            {
                var refTime = ev.LastResumedAtUtc ?? ev.StartedAtUtc;
                ev.AccumulatedElapsedSeconds += (int)Math.Max(0, (DateTime.UtcNow - refTime).TotalSeconds);
                ev.IsPaused = true;
                ev.LastResumedAtUtc = null;
            }
            else if (!wantsPause && ev.IsPaused)
            {
                ev.IsPaused = false;
                ev.LastResumedAtUtc = DateTime.UtcNow;
            }
        }

        if (request.LastResumedAtUtc.HasValue)
        {
            ev.LastResumedAtUtc = request.LastResumedAtUtc.Value;
        }

        if (request.Mode is string newMode && !string.IsNullOrWhiteSpace(newMode))
        {
            var cleanMode = string.Equals(newMode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";
            if (cleanMode == "active")
            {
                var instance = await dbContext.GameInstances
                    .Include(gi => gi.Events)
                    .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

                if (instance is not null)
                {
                    MakeAllEventsPassive(instance, excludeEventId: ev.Id);
                }
            }
            ev.Mode = cleanMode;
        }

        if (request.CurrentSlideIndex.HasValue && request.CurrentSlideIndex.Value >= 0)
        {
            ev.CurrentSlideIndex = request.CurrentSlideIndex.Value;
        }

        if (request.IsHidden.HasValue)
        {
            ev.IsHidden = request.IsHidden.Value;
        }

        if (request.Status is string newStatus && !string.IsNullOrWhiteSpace(newStatus))
        {
            ev.Status = newStatus;
            if (newStatus == "active")
            {
                ev.StartedAtUtc = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> CreateSpontaneousEventAsync(
        Guid projectId,
        CreateSpontaneousEventRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, spontane Events zu erstellen."));
        }

        var instance = await dbContext.GameInstances
            .Include(gi => gi.Events)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        if (string.Equals(request.SlideType, "pantomime", StringComparison.OrdinalIgnoreCase)
            && !IsValidPantomimeConfiguration(request.Parameters))
        {
            return Results.BadRequest(new ApiError("Pantomime benötigt eine Kategorie, 3–100 Begriffe (je maximal 120 Zeichen) und eine Rundendauer von 10 bis 600 Sekunden."));
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";
        string navMode = string.Equals(request.NavigationMode, "self_paced", StringComparison.OrdinalIgnoreCase) ? "self_paced" : "host_controlled";
        bool isHidden = request.IsHidden ?? false;
        DateTime? schedUtc = request.ScheduledStartUtc;
        bool autoStart = request.AutoStartOnSchedule ?? false;
        string status = (schedUtc.HasValue && schedUtc.Value > DateTime.UtcNow) ? "scheduled" : "active";

        if (mode == "active" && status == "active")
        {
            MakeAllEventsPassive(instance);
        }

        string eventTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title.Trim() : ReadTitleFromParameters(request.Parameters, request.SlideType);
        var slideId = Guid.NewGuid();

        var slidesPayload = new[]
        {
            new
            {
                slideId = slideId,
                type = request.SlideType.Trim().ToLowerInvariant(),
                position = 0,
                parameters = request.Parameters
            }
        };

        var newEvent = new GameInstanceEvent
        {
            GameInstanceId = instance.Id,
            Title = eventTitle,
            Subtitle = request.Subtitle,
            Mode = mode,
            Status = status,
            NavigationMode = navMode,
            CurrentSlideIndex = 0,
            IsHidden = isHidden,
            ScheduledStartUtc = schedUtc,
            AutoStartOnSchedule = autoStart,
            SlidesJson = JsonSerializer.Serialize(slidesPayload)
        };

        dbContext.GameInstanceEvents.Add(newEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new { eventId = newEvent.Id });
    }

    private static async Task<IResult> SetSlideStateAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        SetSlideStateRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var gameEvent = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }

        if (request.State.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new ApiError("Der Spielzustand muss ein JSON-Objekt sein."));
        }

        if (JsonNode.Parse(gameEvent.SlidesJson) is not JsonArray slides)
        {
            return Results.NotFound();
        }

        var slide = slides.OfType<JsonObject>()
            .FirstOrDefault(s => string.Equals(s["slideId"]?.GetValue<string>(), slideId.ToString(), StringComparison.OrdinalIgnoreCase));
        if (slide is null)
        {
            return Results.NotFound();
        }

        var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
        parameters["state"] = JsonNode.Parse(request.State.GetRawText());
        slide["parameters"] = parameters;
        gameEvent.SlidesJson = slides.ToJsonString();

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> UpdateEventModeAsync(
        Guid projectId,
        Guid eventId,
        UpdateEventModeRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, den Event-Modus zu ändern."));
        }

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        var cleanMode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";
        if (cleanMode == "active")
        {
            var instance = await dbContext.GameInstances
                .Include(gi => gi.Events)
                .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

            if (instance is not null)
            {
                MakeAllEventsPassive(instance, excludeEventId: ev.Id);
            }
        }

        ev.Mode = cleanMode;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> CloseEventAsync(
        Guid projectId,
        Guid eventId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, Events zu schließen."));
        }

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        ev.Status = "completed";
        ev.ClosedAtUtc = DateTime.UtcNow;

        var instance = await dbContext.GameInstances.FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);
        if (instance is not null && instance.ActiveCanvasEventId == eventId)
        {
            instance.ActiveCanvasEventId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> SetCanvasEventAsync(
        Guid projectId,
        SetCanvasEventRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, den Beamer-Inhalt festzulegen."));
        }

        var instance = await dbContext.GameInstances.FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);
        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        instance.ActiveCanvasEventId = request.EventId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> SubmitEventResponseAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        SubmitResponseRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null)
        {
            return Results.Unauthorized();
        }

        var ev = await dbContext.GameInstanceEvents
            .Include(e => e.GameInstance)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        var newResponse = new GameInstanceEventResponse
        {
            GameInstanceEventId = ev.Id,
            SlideId = slideId,
            ParticipantId = access.ParticipantId ?? Guid.Empty,
            ResponseJson = JsonSerializer.Serialize(request.ResponseData),
            SubmittedAtUtc = DateTime.UtcNow
        };

        dbContext.GameInstanceEventResponses.Add(newResponse);
        await dbContext.SaveChangesAsync(cancellationToken);

        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok();
    }

    private static void MakeAllEventsPassive(GameInstance instance, Guid? excludeEventId = null)
    {
        foreach (var ev in instance.Events)
        {
            if (ev.Status == "active" && (excludeEventId == null || ev.Id != excludeEventId.Value))
            {
                ev.Mode = "passive";
            }
        }
    }

    private static string GetSlideType(Slide slide) => ShowSlideEndpoints.GetSlideType(slide);

    private static string ReadTitleFromParameters(JsonElement parameters, string slideType)
    {
        if (parameters.ValueKind == JsonValueKind.Object)
        {
            if (parameters.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                var titleStr = titleProp.GetString();
                if (!string.IsNullOrWhiteSpace(titleStr))
                {
                    return titleStr.Trim();
                }
            }
            if (parameters.TryGetProperty("question", out var qProp) && qProp.ValueKind == JsonValueKind.String)
            {
                var qStr = qProp.GetString();
                if (!string.IsNullOrWhiteSpace(qStr))
                {
                    return qStr.Trim();
                }
            }
        }

        return slideType switch
        {
            "poll" => "Abstimmung",
            "quiz" => "Quizfragen",
            "ranking" => "Zwischenstand",
            "estimation" => "Schätzfrage",
            "buzzer" => "Buzzer-Runde",
            "hitster" => "Hitster",
            "jeopardy" => "Jeopardy",
            "text" => "Freitext-Antworten",
            "word_cloud" => "Wortwolke",
            "slider" => "Schieberegler",
            "rating" => "Sterne-Bewertung",
            "ordering" => "Sortierung",
            "countdown" => "Countdown-Timer",
            "point_award" => "Punktevergabe",
            _ => "Spontanes Event"
        };
    }
}
