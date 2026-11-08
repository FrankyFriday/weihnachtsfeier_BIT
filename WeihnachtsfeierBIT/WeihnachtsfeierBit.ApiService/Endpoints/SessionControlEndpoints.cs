using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class SessionControlEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/session/state", GetSessionStateAsync).WithName("GetSessionState");
        reader.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/responses", SubmitEventResponseAsync).WithName("SubmitEventResponse");

        admin.MapPost("/session/shows", StartShowInSessionAsync).WithName("StartShowInSession");
        admin.MapPut("/session/events/{eventId:guid}", UpdateSessionEventAsync).WithName("UpdateSessionEvent");
        admin.MapPut("/session/events/{eventId:guid}/mode", UpdateEventModeAsync).WithName("UpdateEventMode");
        admin.MapPut("/session/events/{eventId:guid}/close", CloseEventAsync).WithName("CloseEvent");
        admin.MapPut("/session/canvas", SetCanvasEventAsync).WithName("SetCanvasEvent");
        admin.MapPost("/session/events", CreateSpontaneousEventAsync).WithName("CreateSpontaneousEvent");
        admin.MapPost("/session/toggle-pause", ToggleSessionPauseAsync).WithName("ToggleSessionPause");
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
