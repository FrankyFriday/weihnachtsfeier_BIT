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
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
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

        if (mode == "active")
        {
            MakeAllEventsPassive(instance);
        }

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
            Title = show.Name,
            Mode = mode,
            Status = "active",
            NavigationMode = "host_controlled",
            CurrentSlideIndex = 0,
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
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameInstance.ProjektId == projectId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";

        if (mode == "active")
        {
            var instance = await dbContext.GameInstances
                .Include(gi => gi.Events)
                .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

            if (instance is not null)
            {
                MakeAllEventsPassive(instance, excludeEventId: ev.Id);
            }
        }

        ev.Mode = mode;
        if (request.CurrentSlideIndex.HasValue && request.CurrentSlideIndex.Value >= 0)
        {
            ev.CurrentSlideIndex = request.CurrentSlideIndex.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> CreateSpontaneousEventAsync(
        Guid projectId,
        CreateSpontaneousEventRequest request,
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

        var instance = await dbContext.GameInstances
            .Include(gi => gi.Events)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";

        if (mode == "active")
        {
            MakeAllEventsPassive(instance);
        }

        string title = ReadTitleFromParameters(request.Parameters, request.SlideType);
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
            Title = title,
            Mode = mode,
            Status = "active",
            NavigationMode = "host_controlled",
            CurrentSlideIndex = 0,
            SlidesJson = JsonSerializer.Serialize(slidesPayload)
        };

        dbContext.GameInstanceEvents.Add(newEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Created($"/api/projects/{projectId}/session/events/{newEvent.Id}", new { eventId = newEvent.Id });
    }

    private static async Task<IResult> UpdateEventModeAsync(
        Guid projectId,
        Guid eventId,
        UpdateEventModeRequest request,
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

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";

        if (mode == "active")
        {
            var instance = await dbContext.GameInstances
                .Include(gi => gi.Events)
                .FirstOrDefaultAsync(gi => gi.Id == ev.GameInstanceId, cancellationToken);

            if (instance is not null)
            {
                MakeAllEventsPassive(instance, excludeEventId: ev.Id);
            }
        }

        ev.Mode = mode;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> CloseEventAsync(
        Guid projectId,
        Guid eventId,
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

        var ev = await dbContext.GameInstanceEvents
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (ev is null)
        {
            return Results.NotFound();
        }

        ev.Status = "completed";
        ev.ClosedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.NoContent();
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
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (ev is null || ev.Status != "active")
        {
            return Results.NotFound(new ApiError("Event ist nicht aktiv."));
        }

        Guid participantId;
        if (access.ParticipantId is Guid pId)
        {
            participantId = pId;
        }
        else
        {
            var adminParticipant = await dbContext.Teilnehmer
                .FirstOrDefaultAsync(t => t.ProjektId == projectId && t.Nummer == 0, cancellationToken);

            if (adminParticipant is null)
            {
                var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
                adminParticipant = new Teilnehmer
                {
                    ProjektId = projectId,
                    Projekt = project!,
                    Nummer = 0,
                    Name = "Admin (Test)"
                };
                dbContext.Teilnehmer.Add(adminParticipant);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            participantId = adminParticipant.Id;
        }

        var existingResponse = await dbContext.GameInstanceEventResponses
            .FirstOrDefaultAsync(r => r.GameInstanceEventId == eventId && r.SlideId == slideId && r.ParticipantId == participantId, cancellationToken);

        if (existingResponse is not null)
        {
            existingResponse.ResponseJson = request.ResponseData.GetRawText();
            existingResponse.SubmittedAtUtc = DateTime.UtcNow;
        }
        else
        {
            var newResponse = new GameInstanceEventResponse
            {
                GameInstanceEventId = eventId,
                SlideId = slideId,
                ParticipantId = participantId,
                ResponseJson = request.ResponseData.GetRawText(),
                SubmittedAtUtc = DateTime.UtcNow
            };
            dbContext.GameInstanceEventResponses.Add(newResponse);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> SetCanvasEventAsync(
        Guid projectId,
        SetCanvasEventRequest request,
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

        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        instance.ActiveCanvasEventId = request.EventId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static void MakeAllEventsPassive(GameInstance instance, Guid? excludeEventId = null)
    {
        foreach (var ev in instance.Events)
        {
            if (ev.Id != excludeEventId && ev.Status == "active")
            {
                ev.Mode = "passive";
            }
        }
    }

    private static string GetSlideType(Slide slide) => slide switch
    {
        PollSlide => "poll",
        AnnouncementSlide => "announcement",
        QuizSlide => "quiz",
        RankingSlide => "ranking",
        EstimationSlide => "estimation",
        BuzzerSlide => "buzzer",
        _ => "announcement"
    };

    private static string ReadTitleFromParameters(JsonElement elem, string type)
    {
        if (elem.ValueKind == JsonValueKind.Object)
        {
            if (elem.TryGetProperty("internalName", out var internalName) && internalName.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(internalName.GetString()))
            {
                return internalName.GetString()!;
            }
            if (elem.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
            if (elem.TryGetProperty("question", out var question) && question.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(question.GetString()))
            {
                return question.GetString()!;
            }
        }

        return type switch
        {
            "announcement" => "Ankündigung",
            "poll" => "Abstimmung",
            "quiz" => "Quiz",
            "ranking" => "Zwischenstand",
            "estimation" => "Schätzfrage",
            "buzzer" => "Buzzer",
            _ => "Event"
        };
    }
}
