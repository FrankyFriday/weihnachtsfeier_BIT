using System.Text.Json;
using System.Text.Json.Nodes;
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
        PantomimeEndpoints.Map(reader, admin);
        HitsterEndpoints.Map(reader, admin);
        reader.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/responses", SubmitEventResponseAsync).WithName("SubmitEventResponse");
        reader.MapPut("/session/events/{eventId:guid}/slides/{slideId:guid}/responses/team", UpdateTeamEventResponseAsync).WithName("UpdateTeamEventResponse");

        admin.MapPost("/session/shows", StartShowInSessionAsync).WithName("StartShowInSession");
        admin.MapPut("/session/events/{eventId:guid}", UpdateSessionEventAsync).WithName("UpdateSessionEvent");
        admin.MapPut("/session/events/{eventId:guid}/slides/{slideId:guid}/state", SetSlideStateAsync).WithName("SetSlideState");
        admin.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/workflow", UpdateQuestionWorkflowAsync).WithName("UpdateQuestionWorkflow");
        admin.MapPut("/session/events/{eventId:guid}/mode", UpdateEventModeAsync).WithName("UpdateEventMode");
        admin.MapPut("/session/events/{eventId:guid}/close", CloseEventAsync).WithName("CloseEvent");
        admin.MapPut("/session/canvas", SetCanvasEventAsync).WithName("SetCanvasEvent");
        admin.MapPut("/session/canvas-settings", SetCanvasDisplaySettingsAsync).WithName("SetCanvasDisplaySettings");
        admin.MapPost("/session/events", CreateSpontaneousEventAsync).WithName("CreateSpontaneousEvent");
        admin.MapPost("/session/toggle-pause", ToggleSessionPauseAsync).WithName("ToggleSessionPause");
        admin.MapPut("/session/events/{eventId:guid}/responses/{responseId:guid}", AdminUpdateResponseAsync).WithName("AdminUpdateResponse");
        admin.MapDelete("/session/events/{eventId:guid}/responses/{responseId:guid}", AdminDeleteResponseAsync).WithName("AdminDeleteResponse");
        admin.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/responses/override", AdminOverrideResponseAsync).WithName("AdminOverrideResponse");
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

        if (parameters.TryGetProperty("maxSkipsPerPerformer", out var skipLimit)
            && (skipLimit.ValueKind != JsonValueKind.Number
                || !skipLimit.TryGetInt32(out var maxSkips)
                || maxSkips is < 0 or > 100))
        {
            return false;
        }

        var cleanTerms = terms.EnumerateArray()
            .Where(term => term.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(term.GetString()))
            .Select(term => term.GetString()!.Trim())
            .ToArray();
        return cleanTerms.Length is >= 3 and <= 100
            && cleanTerms.All(term => term.Length <= 120)
            && cleanTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count() == cleanTerms.Length;
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
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var state = await GameStateNotifier.BuildProjectGameStateAsync(projectId, dbContext, cancellationToken);
        if (state is null)
        {
            return Results.NotFound();
        }

        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (access.ParticipantId is Guid participantId)
        {
            var teamId = await dbContext.Teilnehmer.AsNoTracking()
                .Where(participant => participant.Id == participantId && participant.ProjektId == projectId)
                .Select(participant => participant.TeamId)
                .FirstOrDefaultAsync(cancellationToken);
            state = QuestionWorkflowService.FilterProjectState(state, teamId);
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
            return Results.BadRequest(new ApiError("Pantomime benötigt eine Kategorie, 3–100 eindeutige Begriffe (je maximal 120 Zeichen), eine Rundendauer von 10 bis 600 Sekunden und ein optionales Skip-Limit von 0 bis 100."));
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

    private static async Task<IResult> SetCanvasDisplaySettingsAsync(
        Guid projectId,
        WeihnachtsfeierBit.Framework.CanvasDisplaySettings? settings,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Beamer-Einstellungen anzupassen."));
        }

        if (settings?.IsValid != true)
        {
            return Results.BadRequest(new ApiError("Die Beamer-Einstellungen sind ungültig."));
        }

        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);
        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        instance.CanvasSettingsJson = settings.ToJson();
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
        ParticipantPermissionService permissionService,
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
        if (access.ParticipantId is not null &&
            !await permissionService.AllowsAsync(
                access,
                WeihnachtsfeierBit.Framework.ParticipantPermissionKeys.IsMuted,
                requireInteraction: false,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var ev = await dbContext.GameInstanceEvents
            .Include(e => e.GameInstance)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        var slides = ParseSlides(ev);
        var slide = FindSlide(slides, slideId);
        if (slide is null)
        {
            return Results.NotFound(new ApiError("Die Folie gehört nicht zu diesem Event."));
        }

        if (QuestionWorkflowService.IsQuestionSlide(slide))
        {
            if (access.ParticipantId is not Guid participantId)
            {
                return Results.Forbid();
            }

            if (!IsCurrentSlide(ev, slides, slideId) || ev.GameInstance.IsPaused)
            {
                return Results.Conflict(new ApiError("Diese Frage nimmt aktuell keine Antworten an."));
            }

            if (QuestionWorkflowService.GetPhase(slide) != QuestionWorkflowService.AnswerPhase)
            {
                return Results.Conflict(new ApiError("Antworten sind in dieser Phase gesperrt."));
            }

            if (!ReadBool(QuestionWorkflowService.GetWorkflowState(slide), "sessionPaused")
                && ExpireQuestionTimerIfNeeded(slide))
            {
                ev.SlidesJson = slides.ToJsonString();
                await dbContext.SaveChangesAsync(cancellationToken);
                await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
                return Results.Conflict(new ApiError("Die Antwortzeit ist abgelaufen."));
            }

            if (!IsValidQuestionResponse(request.ResponseData, slide))
            {
                return Results.BadRequest(new ApiError("Die Quizantwort oder Schätzung ist ungültig."));
            }

            if (QuestionWorkflowService.IsTeamExclusive(slide)
                && !await CanParticipantAnswerForActiveTeamAsync(projectId, ev, participantId, dbContext, cancellationToken))
            {
                return Results.Forbid();
            }

            var existingResponse = await dbContext.GameInstanceEventResponses
                .FirstOrDefaultAsync(
                    response => response.GameInstanceEventId == ev.Id
                        && response.SlideId == slideId
                        && response.ParticipantId == participantId,
                    cancellationToken);
            if (existingResponse is null)
            {
                dbContext.GameInstanceEventResponses.Add(new GameInstanceEventResponse
                {
                    GameInstanceEventId = ev.Id,
                    SlideId = slideId,
                    ParticipantId = participantId,
                    ResponseJson = request.ResponseData.GetRawText(),
                    SubmittedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existingResponse.ResponseJson = request.ResponseData.GetRawText();
                existingResponse.SubmittedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
            return Results.Ok();
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

    private static async Task<IResult> UpdateTeamEventResponseAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        UpdateTeamEventResponseRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        ParticipantPermissionService permissionService,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access?.ParticipantId is not Guid participantId)
        {
            return access is null ? Results.Unauthorized() : Results.Forbid();
        }

        if (!await permissionService.AllowsAsync(
                access,
                WeihnachtsfeierBit.Framework.ParticipantPermissionKeys.IsMuted,
                requireInteraction: false,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var gameEvent = await dbContext.GameInstanceEvents
            .Include(item => item.GameInstance)
            .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }

        var slides = ParseSlides(gameEvent);
        var slide = FindSlide(slides, slideId);
        if (slide is null
            || !QuestionWorkflowService.IsQuestionSlide(slide)
            || !QuestionWorkflowService.IsTeamExclusive(slide))
        {
            return Results.BadRequest(new ApiError("Gemeinsam bearbeitbare Teamantworten sind nur für team-exklusive Quiz- und Schätzfragen verfügbar."));
        }
        if (!QuestionWorkflowService.CanEditTeamResponse(
                ReadString(slide, "type"),
                participantId,
                request.ParticipantId))
        {
            return Results.Forbid();
        }

        if (!IsCurrentSlide(gameEvent, slides, slideId) || gameEvent.GameInstance.IsPaused)
        {
            return Results.Conflict(new ApiError("Diese Frage nimmt aktuell keine Antworten an."));
        }

        if (QuestionWorkflowService.GetPhase(slide) != QuestionWorkflowService.AnswerPhase)
        {
            return Results.Conflict(new ApiError("Antworten sind in dieser Phase gesperrt."));
        }

        if (!ReadBool(QuestionWorkflowService.GetWorkflowState(slide), "sessionPaused")
            && ExpireQuestionTimerIfNeeded(slide))
        {
            gameEvent.SlidesJson = slides.ToJsonString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
            return Results.Conflict(new ApiError("Die Antwortzeit ist abgelaufen."));
        }

        if (!IsValidQuestionResponse(request.ResponseData, slide))
        {
            return Results.BadRequest(new ApiError("Die Quizantwort oder Schätzung ist ungültig."));
        }

        var teamId = await dbContext.Teilnehmer.AsNoTracking()
            .Where(item => item.Id == participantId && item.ProjektId == projectId)
            .Select(item => item.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        var targetTeamId = await dbContext.Teilnehmer.AsNoTracking()
            .Where(item => item.Id == request.ParticipantId && item.ProjektId == projectId)
            .Select(item => item.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        var activeTeamId = QuestionWorkflowService.GetActiveTeamId(gameEvent);
        if (!teamId.HasValue || teamId != targetTeamId || teamId != activeTeamId)
        {
            return Results.Forbid();
        }

        var response = await dbContext.GameInstanceEventResponses
            .FirstOrDefaultAsync(
                item => item.GameInstanceEventId == eventId
                    && item.SlideId == slideId
                    && item.ParticipantId == request.ParticipantId,
                cancellationToken);
        if (response is null)
        {
            response = new GameInstanceEventResponse
            {
                GameInstanceEventId = eventId,
                SlideId = slideId,
                ParticipantId = request.ParticipantId
            };
            dbContext.GameInstanceEventResponses.Add(response);
        }

        response.ResponseJson = request.ResponseData.GetRawText();
        response.SubmittedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateQuestionWorkflowAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        UpdateQuestionWorkflowRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, Quiz- oder Schätzfragen zu steuern."));
        }

        var gameEvent = await dbContext.GameInstanceEvents
            .Include(item => item.Responses)
                .ThenInclude(response => response.Participant)
                    .ThenInclude(participant => participant.Team)
            .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }

        var slides = ParseSlides(gameEvent);
        var slide = FindSlide(slides, slideId);
        if (slide is null || !QuestionWorkflowService.IsQuestionSlide(slide))
        {
            return Results.BadRequest(new ApiError("Die Folie ist kein Quiz und keine Schätzfrage."));
        }

        var now = DateTimeOffset.UtcNow;
        var workflow = QuestionWorkflowService.GetWorkflowState(slide);
        var phase = QuestionWorkflowService.GetPhase(slide);
        var parameters = QuestionWorkflowService.GetParameters(slide);
        var action = request.Action.Trim().ToLowerInvariant();
        var recalculateScore = false;

        switch (action)
        {
            case "open-answers":
            case "reopen-answers":
                QuestionWorkflowService.StartPhase(
                    slide,
                    QuestionWorkflowService.AnswerPhase,
                    QuestionWorkflowService.GetConfiguredSeconds(slide, "timeLimitSeconds"),
                    now);
                break;
            case "reveal":
                if (QuestionWorkflowService.IsTeamExclusive(slide)
                    && !QuestionWorkflowService.HasOfficialTeamResult(ReadString(slide, "type"), workflow))
                {
                    return Results.Conflict(new ApiError("Vor der Ergebnisfreigabe muss eine offizielle Teamantwort festgelegt werden."));
                }
                workflow["phase"] = QuestionWorkflowService.ResultsPhase;
                workflow["timerEndsAtUtc"] = null;
                workflow["timerPaused"] = false;
                workflow["remainingSeconds"] = 0;
                workflow["phaseStartedAtUtc"] = now.ToString("O");
                recalculateScore = true;
                break;
            case "back-to-question":
                QuestionWorkflowService.StartPhase(
                    slide,
                    QuestionWorkflowService.QuestionPhase,
                    QuestionWorkflowService.GetConfiguredSeconds(slide, "questionTimeLimitSeconds"),
                    now);
                break;
            case "pause-timer":
                if (phase is not (QuestionWorkflowService.QuestionPhase or QuestionWorkflowService.AnswerPhase)
                    || QuestionWorkflowService.GetTimerEnd(slide) is null
                    || ReadBool(workflow, "timerPaused"))
                {
                    return Results.Conflict(new ApiError("Für diese Phase läuft kein Timer, der pausiert werden kann."));
                }
                workflow["remainingSeconds"] = QuestionWorkflowService.GetRemainingSeconds(slide, now);
                workflow["timerPaused"] = true;
                workflow["timerEndsAtUtc"] = null;
                break;
            case "resume-timer":
                if (!ReadBool(workflow, "timerPaused") || ReadBool(workflow, "sessionPaused"))
                {
                    return Results.Conflict(new ApiError("Der Timer kann aktuell nicht fortgesetzt werden."));
                }
                var pausedSeconds = Math.Max(0, ReadInt(workflow, "remainingSeconds") ?? 0);
                workflow["timerPaused"] = false;
                workflow["timerEndsAtUtc"] = pausedSeconds > 0 ? now.AddSeconds(pausedSeconds).ToString("O") : now.ToString("O");
                break;
            case "reset-timer":
                var resetSeconds = phase switch
                {
                    QuestionWorkflowService.QuestionPhase => QuestionWorkflowService.GetConfiguredSeconds(slide, "questionTimeLimitSeconds"),
                    QuestionWorkflowService.AnswerPhase => QuestionWorkflowService.GetConfiguredSeconds(slide, "timeLimitSeconds"),
                    _ => null
                };
                if (!resetSeconds.HasValue)
                {
                    return Results.BadRequest(new ApiError("Für diese Phase ist kein Timer konfiguriert."));
                }
                QuestionWorkflowService.StartPhase(slide, phase, resetSeconds, now);
                break;
            case "set-timer":
                if (phase is not (QuestionWorkflowService.QuestionPhase or QuestionWorkflowService.AnswerPhase)
                    || request.Seconds is < 0 or > 3600)
                {
                    return Results.BadRequest(new ApiError("Der Timerwert muss zwischen 0 und 3600 Sekunden liegen."));
                }
                QuestionWorkflowService.StartPhase(slide, phase, request.Seconds, now);
                if (request.Seconds == 0)
                {
                    QuestionWorkflowService.TickActiveSlide(slide, now);
                }
                break;
            case "set-official-answer":
                if (!string.Equals(ReadString(slide, "type"), "quiz", StringComparison.OrdinalIgnoreCase)
                    || request.OfficialAnswerIndex is not int answerIndex
                    || answerIndex < 0
                    || !TryReadStringArray(parameters, "answers", out var answers)
                    || answerIndex >= answers.Length)
                {
                    return Results.BadRequest(new ApiError("Die offizielle Quizantwort ist ungültig."));
                }
                workflow["officialAnswerIndex"] = answerIndex;
                recalculateScore = phase == QuestionWorkflowService.ResultsPhase;
                break;
            case "clear-official-answer":
                if (!string.Equals(ReadString(slide, "type"), "quiz", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new ApiError("Nur Quizfragen unterstützen eine offizielle Antwort."));
                }
                workflow.Remove("officialAnswerIndex");
                recalculateScore = phase == QuestionWorkflowService.ResultsPhase;
                break;
            case "set-official-estimation":
                if (!string.Equals(ReadString(slide, "type"), "estimation", StringComparison.OrdinalIgnoreCase)
                    || request.OfficialEstimationValue is not double estimate
                    || double.IsNaN(estimate)
                    || double.IsInfinity(estimate))
                {
                    return Results.BadRequest(new ApiError("Der offizielle Schätzwert ist ungültig."));
                }
                workflow["officialEstimationValue"] = estimate;
                break;
            default:
                return Results.BadRequest(new ApiError("Unbekannte Aktion für Quiz oder Schätzfrage."));
        }

        if (recalculateScore)
        {
            await ReconcileQuizScoresAsync(projectId, gameEvent, slide, workflow, dbContext, cancellationToken);
        }

        gameEvent.SlidesJson = slides.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok();
    }

    private static async Task ReconcileQuizScoresAsync(
        Guid projectId,
        GameInstanceEvent gameEvent,
        JsonObject slide,
        JsonObject workflow,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentAwards = new Dictionary<string, (Guid TeamId, int Points)>(StringComparer.Ordinal);
        var parameters = QuestionWorkflowService.GetParameters(slide);
        var isQuiz = string.Equals(ReadString(slide, "type"), "quiz", StringComparison.OrdinalIgnoreCase);
        var teamExclusive = QuestionWorkflowService.IsTeamExclusive(slide);
        var points = Math.Max(0, ReadInt(parameters, "points") ?? 0);
        var slideId = Guid.TryParse(ReadString(slide, "slideId"), out var parsedSlideId) ? parsedSlideId : Guid.Empty;
        var responses = gameEvent.Responses.Where(response => response.SlideId == slideId).ToArray();

        if (isQuiz)
        {
            var correctIndices = TryReadIntArray(parameters, "correctAnswerIndices");
            int? officialAnswer = ReadInt(workflow, "officialAnswerIndex");
            if (teamExclusive)
            {
                var activeTeamId = QuestionWorkflowService.GetActiveTeamId(gameEvent);
                if (activeTeamId.HasValue)
                {
                    var teamResponses = responses
                        .Where(response => response.Participant.TeamId == activeTeamId)
                        .Select(response => (Response: response, Answer: ReadResponseInt(response.ResponseJson, "answerIndex")))
                        .Where(item => item.Answer.HasValue)
                        .ToArray();
                    var selectedAnswer = officialAnswer ?? GetUniqueMajorityAnswer(teamResponses.Select(item => item.Answer!.Value));
                    if (selectedAnswer.HasValue)
                    {
                        workflow["officialAnswerIndex"] = selectedAnswer.Value;
                        if (correctIndices.Contains(selectedAnswer.Value))
                        {
                            currentAwards[$"team:{activeTeamId.Value}"] = (activeTeamId.Value, points);
                        }
                    }
                }
            }
            else
            {
                foreach (var response in responses)
                {
                    var answer = ReadResponseInt(response.ResponseJson, "answerIndex");
                    if (response.Participant.TeamId is Guid teamId && answer.HasValue && correctIndices.Contains(answer.Value))
                    {
                        currentAwards[$"response:{response.Id}"] = (teamId, points);
                    }
                }
            }
        }
        else if (!teamExclusive && points > 0)
        {
            var targetValue = ReadDouble(parameters, "correctValue");
            var estimates = responses
                .Where(response => response.Participant.TeamId.HasValue)
                .Select(response => (TeamId: response.Participant.TeamId!.Value, Value: ReadResponseDouble(response.ResponseJson, "estimationValue")))
                .Where(item => item.Value.HasValue)
                .ToArray();
            var teamDistances = estimates
                .GroupBy(item => item.TeamId)
                .Select(group => (TeamId: group.Key, Difference: group.Min(item => Math.Abs(item.Value!.Value - targetValue))))
                .ToArray();
            if (teamDistances.Length > 0)
            {
                var closestDifference = teamDistances.Min(item => item.Difference);
                foreach (var team in teamDistances.Where(item => Math.Abs(item.Difference - closestDifference) < 0.0000001))
                {
                    currentAwards[$"team:{team.TeamId}"] = (team.TeamId, points);
                }
            }
        }

        var previousAwards = ReadStoredAwards(workflow);
        var oldTotals = previousAwards.Values
            .GroupBy(award => award.TeamId)
            .ToDictionary(group => group.Key, group => group.Sum(award => award.Points));
        var newTotals = currentAwards.Values
            .GroupBy(award => award.TeamId)
            .ToDictionary(group => group.Key, group => group.Sum(award => award.Points));

        foreach (var teamId in oldTotals.Keys.Union(newTotals.Keys))
        {
            var delta = newTotals.GetValueOrDefault(teamId) - oldTotals.GetValueOrDefault(teamId);
            if (delta == 0)
            {
                continue;
            }

            var team = await dbContext.Teams.FirstOrDefaultAsync(
                item => item.Id == teamId && item.ProjektId == projectId,
                cancellationToken);
            if (team is null)
            {
                continue;
            }

            var oldPoints = team.Punkte;
            team.Punkte = Math.Max(0, oldPoints + delta);
            dbContext.TeamPointLogs.Add(new TeamPointLog
            {
                ProjektId = projectId,
                TeamId = teamId,
                DeltaPoints = team.Punkte - oldPoints,
                NewTotalPoints = team.Punkte,
                Reason = isQuiz ? $"Quiz: {gameEvent.Title}" : $"Schätzfrage: {gameEvent.Title}",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var awards = new JsonArray();
        foreach (var (key, award) in currentAwards)
        {
            awards.Add(new JsonObject
            {
                ["key"] = key,
                ["teamId"] = award.TeamId.ToString(),
                ["points"] = award.Points
            });
        }
        workflow["scoreAwards"] = awards;
    }

    private static Dictionary<string, (Guid TeamId, int Points)> ReadStoredAwards(JsonObject workflow)
    {
        var result = new Dictionary<string, (Guid TeamId, int Points)>(StringComparer.Ordinal);
        if (workflow["scoreAwards"] is not JsonArray awards)
        {
            return result;
        }

        foreach (var item in awards.OfType<JsonObject>())
        {
            var key = ReadString(item, "key");
            var teamIdText = ReadString(item, "teamId");
            var points = ReadInt(item, "points");
            if (!string.IsNullOrEmpty(key)
                && Guid.TryParse(teamIdText, out var teamId)
                && points is >= 0)
            {
                result[key] = (teamId, points.Value);
            }
        }

        return result;
    }

    private static int? GetUniqueMajorityAnswer(IEnumerable<int> answers)
    {
        var counts = answers.GroupBy(answer => answer)
            .Select(group => (Answer: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count)
            .ToArray();
        return counts.Length > 0 && (counts.Length == 1 || counts[0].Count > counts[1].Count)
            ? counts[0].Answer
            : null;
    }

    private static bool ExpireQuestionTimerIfNeeded(JsonObject slide)
    {
        if (QuestionWorkflowService.GetTimerEnd(slide) is DateTimeOffset timerEnd
            && timerEnd <= DateTimeOffset.UtcNow
            && !ReadBool(QuestionWorkflowService.GetWorkflowState(slide), "timerPaused"))
        {
            QuestionWorkflowService.TickActiveSlide(slide, DateTimeOffset.UtcNow);
            return true;
        }

        return false;
    }

    private static async Task<bool> CanParticipantAnswerForActiveTeamAsync(
        Guid projectId,
        GameInstanceEvent gameEvent,
        Guid participantId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participantTeamId = await dbContext.Teilnehmer.AsNoTracking()
            .Where(item => item.Id == participantId && item.ProjektId == projectId)
            .Select(item => item.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        var activeTeamId = QuestionWorkflowService.GetActiveTeamId(gameEvent);
        return participantTeamId.HasValue && participantTeamId == activeTeamId;
    }

    private static bool IsValidQuestionResponse(JsonElement response, JsonObject slide)
    {
        if (response.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var parameters = QuestionWorkflowService.GetParameters(slide);
        if (string.Equals(ReadString(slide, "type"), "quiz", StringComparison.OrdinalIgnoreCase))
        {
            return response.TryGetProperty("answerIndex", out var answer)
                && answer.ValueKind == JsonValueKind.Number
                && answer.TryGetInt32(out var index)
                && TryReadStringArray(parameters, "answers", out var answers)
                && index >= 0
                && index < answers.Length;
        }

        return response.TryGetProperty("estimationValue", out var estimate)
            && estimate.ValueKind == JsonValueKind.Number
            && estimate.TryGetDouble(out var value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    private static JsonArray ParseSlides(GameInstanceEvent gameEvent) =>
        JsonNode.Parse(gameEvent.SlidesJson) as JsonArray ?? new JsonArray();

    private static JsonObject? FindSlide(JsonArray slides, Guid slideId) =>
        slides.OfType<JsonObject>()
            .FirstOrDefault(item => Guid.TryParse(ReadString(item, "slideId"), out var id) && id == slideId);

    private static bool IsCurrentSlide(GameInstanceEvent gameEvent, JsonArray slides, Guid slideId) =>
        gameEvent.CurrentSlideIndex >= 0
        && gameEvent.CurrentSlideIndex < slides.Count
        && slides[gameEvent.CurrentSlideIndex] is JsonObject currentSlide
        && Guid.TryParse(ReadString(currentSlide, "slideId"), out var currentSlideId)
        && currentSlideId == slideId;

    private static string ReadString(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number)
            ? number
            : null;

    private static double ReadDouble(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var number)
            ? number
            : 0;

    private static bool ReadBool(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;

    private static int? ReadResponseInt(string responseJson, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                    ? number
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double? ReadResponseDouble(string responseJson, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out var number)
                    ? number
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadStringArray(JsonObject value, string propertyName, out string[] result)
    {
        result = value[propertyName] is JsonArray array
            ? array.Select(item => item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text ?? string.Empty : string.Empty).ToArray()
            : [];
        return value[propertyName] is JsonArray;
    }

    private static int[] TryReadIntArray(JsonObject value, string propertyName) =>
        value[propertyName] is JsonArray array
            ? array.Select(item => item is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) ? number : int.MinValue).ToArray()
            : [];

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

    private static async Task<IResult> AdminUpdateResponseAsync(
        Guid projectId,
        Guid eventId,
        Guid responseId,
        AdminUpdateResponseRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession) return Results.BadRequest(new ApiError("Keine Berechtigung."));

        var response = await dbContext.GameInstanceEventResponses
            .FirstOrDefaultAsync(r => r.Id == responseId && r.GameInstanceEventId == eventId, cancellationToken);
        if (response is null) return Results.NotFound();

        response.ResponseJson = request.ResponseData.GetRawText();
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> AdminDeleteResponseAsync(
        Guid projectId,
        Guid eventId,
        Guid responseId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession) return Results.BadRequest(new ApiError("Keine Berechtigung."));

        var response = await dbContext.GameInstanceEventResponses
            .FirstOrDefaultAsync(r => r.Id == responseId && r.GameInstanceEventId == eventId, cancellationToken);
        if (response is null) return Results.NotFound();

        dbContext.GameInstanceEventResponses.Remove(response);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> AdminOverrideResponseAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        AdminOverrideResponseRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession) return Results.BadRequest(new ApiError("Keine Berechtigung."));

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);
        if (ev is null) return Results.NotFound();

        var participant = await dbContext.Teilnehmer
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId && p.ProjektId == projectId, cancellationToken);
        if (participant is null) return Results.NotFound(new ApiError("Teilnehmer nicht gefunden."));

        var existing = await dbContext.GameInstanceEventResponses
            .FirstOrDefaultAsync(r => r.GameInstanceEventId == eventId && r.SlideId == slideId && r.ParticipantId == request.ParticipantId, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.ResponseJson = request.ResponseData.GetRawText();
            existing.SubmittedAtUtc = now;
        }
        else
        {
            var newResp = new GameInstanceEventResponse
            {
                GameInstanceEventId = eventId,
                SlideId = slideId,
                ParticipantId = request.ParticipantId,
                ResponseJson = request.ResponseData.GetRawText(),
                SubmittedAtUtc = now
            };
            dbContext.GameInstanceEventResponses.Add(newResp);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }
}
