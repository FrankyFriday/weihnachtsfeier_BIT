using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class SessionControlEndpoints
{
    public static void Map(RouteGroupBuilder projects)
    {
        projects.MapGet("/session/state", GetSessionStateAsync).WithName("GetSessionState");
        projects.MapPost("/session/shows", StartShowInSessionAsync).WithName("StartShowInSession");
        projects.MapPut("/session/shows/{instanceShowId:guid}", UpdateShowInSessionAsync).WithName("UpdateShowInSession");
        projects.MapDelete("/session/shows/{instanceShowId:guid}", StopShowInSessionAsync).WithName("StopShowInSession");

        projects.MapPost("/session/events", CreateSpontaneousEventAsync).WithName("CreateSpontaneousEvent");
        projects.MapPut("/session/events/{eventId:guid}/mode", UpdateEventModeAsync).WithName("UpdateEventMode");
        projects.MapPut("/session/events/{eventId:guid}/close", CloseEventAsync).WithName("CloseEvent");
    }

    private static async Task<IResult> GetSessionStateAsync(
        Guid projectId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

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
            .Include(gi => gi.Shows)
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
            MakeAllItemsPassive(instance);
        }

        var instanceShow = new GameInstanceShow
        {
            GameInstanceId = instance.Id,
            ShowId = show.Id,
            Mode = mode,
            CurrentSlideIndex = 0
        };

        dbContext.GameInstanceShows.Add(instanceShow);

        var orderedSlides = show.Slides.OrderBy(s => s.Position).ToList();
        if (orderedSlides.Count > 0)
        {
            var firstSlide = orderedSlides[0];
            var title = GetSlideTitle(firstSlide);

            var instanceEvent = new GameInstanceEvent
            {
                GameInstanceId = instance.Id,
                GameInstanceShow = instanceShow,
                SourceSlideId = firstSlide.Id,
                SlideType = GetSlideType(firstSlide),
                Title = title,
                Mode = mode,
                Status = "active",
                ParametersJson = firstSlide.ParametersJson
            };

            dbContext.GameInstanceEvents.Add(instanceEvent);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> UpdateShowInSessionAsync(
        Guid projectId,
        Guid instanceShowId,
        UpdateSessionShowRequest request,
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
            .Include(gi => gi.Shows)
            .Include(gi => gi.Events)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.NotFound();
        }

        var instanceShow = await dbContext.GameInstanceShows
            .Include(gis => gis.Show)
                .ThenInclude(s => s.Slides)
            .FirstOrDefaultAsync(gis => gis.Id == instanceShowId && gis.GameInstanceId == instance.Id, cancellationToken);

        if (instanceShow is null)
        {
            return Results.NotFound();
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";

        if (mode == "active")
        {
            MakeAllItemsPassive(instance, excludeShowId: instanceShowId);
        }

        instanceShow.Mode = mode;

        var orderedSlides = instanceShow.Show.Slides.OrderBy(s => s.Position).ToList();
        int targetIndex = Math.Clamp(request.CurrentSlideIndex, 0, Math.Max(0, orderedSlides.Count - 1));
        instanceShow.CurrentSlideIndex = targetIndex;

        var previousEvents = await dbContext.GameInstanceEvents
            .Where(e => e.GameInstanceShowId == instanceShowId && e.Status == "active")
            .ToListAsync(cancellationToken);

        foreach (var oldEvent in previousEvents)
        {
            oldEvent.Status = "completed";
            oldEvent.ClosedAtUtc = DateTime.UtcNow;
        }

        if (orderedSlides.Count > targetIndex)
        {
            var targetSlide = orderedSlides[targetIndex];
            var title = GetSlideTitle(targetSlide);

            var newEvent = new GameInstanceEvent
            {
                GameInstanceId = instance.Id,
                GameInstanceShowId = instanceShow.Id,
                SourceSlideId = targetSlide.Id,
                SlideType = GetSlideType(targetSlide),
                Title = title,
                Mode = mode,
                Status = "active",
                ParametersJson = targetSlide.ParametersJson
            };

            dbContext.GameInstanceEvents.Add(newEvent);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> StopShowInSessionAsync(
        Guid projectId,
        Guid instanceShowId,
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

        var instanceShow = await dbContext.GameInstanceShows
            .Include(gis => gis.Events)
            .FirstOrDefaultAsync(gis => gis.Id == instanceShowId, cancellationToken);

        if (instanceShow is null)
        {
            return Results.NotFound();
        }

        foreach (var ev in instanceShow.Events.Where(e => e.Status == "active"))
        {
            ev.Status = "completed";
            ev.ClosedAtUtc = DateTime.UtcNow;
        }

        dbContext.GameInstanceShows.Remove(instanceShow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.NoContent();
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
            .Include(gi => gi.Shows)
            .Include(gi => gi.Events)
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.BadRequest(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        var mode = string.Equals(request.Mode, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "passive";

        if (mode == "active")
        {
            MakeAllItemsPassive(instance);
        }

        string title = ReadTitleFromParameters(request.Parameters, request.SlideType);

        var newEvent = new GameInstanceEvent
        {
            GameInstanceId = instance.Id,
            SlideType = request.SlideType.Trim().ToLowerInvariant(),
            Title = title,
            Mode = mode,
            Status = "active",
            ParametersJson = request.Parameters.GetRawText()
        };

        dbContext.GameInstanceEvents.Add(newEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Created($"/api/projects/{projectId}/session/events/{newEvent.Id}", new { newEvent.Id });
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
                .Include(gi => gi.Shows)
                .Include(gi => gi.Events)
                .FirstOrDefaultAsync(gi => gi.Id == ev.GameInstanceId, cancellationToken);

            if (instance is not null)
            {
                MakeAllItemsPassive(instance, excludeEventId: ev.Id);
            }
        }

        ev.Mode = mode;
        if (ev.GameInstanceShowId is Guid showInstId)
        {
            var showInst = await dbContext.GameInstanceShows.FirstOrDefaultAsync(gis => gis.Id == showInstId, cancellationToken);
            if (showInst is not null)
            {
                showInst.Mode = mode;
            }
        }

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

    private static void MakeAllItemsPassive(GameInstance instance, Guid? excludeShowId = null, Guid? excludeEventId = null)
    {
        foreach (var show in instance.Shows)
        {
            if (show.Id != excludeShowId)
            {
                show.Mode = "passive";
            }
        }

        foreach (var ev in instance.Events)
        {
            if (ev.Id != excludeEventId && ev.Status == "active")
            {
                ev.Mode = "passive";
            }
        }
    }

    private static string GetSlideTitle(Slide slide)
    {
        try
        {
            using var doc = JsonDocument.Parse(slide.ParametersJson);
            return ReadTitleFromParameters(doc.RootElement, GetSlideType(slide));
        }
        catch (JsonException)
        {
            return GetSlideType(slide);
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
