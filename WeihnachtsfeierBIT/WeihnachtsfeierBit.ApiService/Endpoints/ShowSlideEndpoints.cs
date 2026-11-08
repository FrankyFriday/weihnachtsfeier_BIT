using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ShowSlideEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/shows/{showId:guid}/slides", GetSlidesAsync).WithName("GetAdminSlides");
        admin.MapPost("/shows/{showId:guid}/slides", CreateSlideAsync).WithName("CreateAdminSlide");
        admin.MapPut("/shows/{showId:guid}/slides/{slideId:guid}", UpdateSlideAsync).WithName("UpdateAdminSlide");
        admin.MapDelete("/shows/{showId:guid}/slides/{slideId:guid}", DeleteSlideAsync).WithName("DeleteAdminSlide");
        admin.MapPut("/shows/{showId:guid}/slide-positions", ReorderSlidesAsync).WithName("ReorderAdminSlides");
    }

    public static async Task<IResult> GetSlidesAsync(
        Guid projectId,
        Guid showId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsShowInProjectAsync(dbContext, projectId, showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var slides = await dbContext.Slides.AsNoTracking()
            .Where(slide => slide.ShowId == showId)
            .OrderBy(slide => slide.Position)
            .ToListAsync(cancellationToken);

        return Results.Ok(slides.Select(ToResponse));
    }

    private static async Task<IResult> CreateSlideAsync(
        Guid projectId,
        Guid showId,
        SlideRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsShowInProjectAsync(dbContext, projectId, showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var show = await dbContext.Shows.FindAsync([showId], cancellationToken);
        if (show is null)
        {
            return Results.NotFound();
        }

        var slideResult = CreateSlide(request, show);
        if (slideResult.Error is not null || slideResult.Slide is null)
        {
            return Results.BadRequest(new ApiError(slideResult.Error ?? "Slide konnte nicht erstellt werden."));
        }

        dbContext.Slides.Add(slideResult.Slide);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/projects/{projectId}/shows/{showId}/slides/{slideResult.Slide.Id}",
            ToResponse(slideResult.Slide));
    }

    private static async Task<IResult> UpdateSlideAsync(
        Guid projectId,
        Guid showId,
        Guid slideId,
        SlideRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsShowInProjectAsync(dbContext, projectId, showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var slide = await dbContext.Slides.FirstOrDefaultAsync(
            item => item.Id == slideId && item.ShowId == showId, cancellationToken);
        if (slide is null)
        {
            return Results.NotFound();
        }

        var updated = ApplyRequest(slide, request);
        if (updated.Error is not null)
        {
            return Results.BadRequest(new ApiError(updated.Error));
        }

        slide.Position = request.Position;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(slide));
    }

    private static async Task<IResult> DeleteSlideAsync(
        Guid projectId,
        Guid showId,
        Guid slideId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsShowInProjectAsync(dbContext, projectId, showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var slide = await dbContext.Slides.FirstOrDefaultAsync(
            item => item.Id == slideId && item.ShowId == showId, cancellationToken);
        if (slide is null)
        {
            return Results.NotFound();
        }

        dbContext.Slides.Remove(slide);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReorderSlidesAsync(
        Guid projectId,
        Guid showId,
        ReorderSlidesRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsShowInProjectAsync(dbContext, projectId, showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var slides = await dbContext.Slides
            .Where(s => s.ShowId == showId)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < request.SlideIds.Length; i++)
        {
            var id = request.SlideIds[i];
            var slide = slides.FirstOrDefault(s => s.Id == id);
            if (slide is not null)
            {
                slide.Position = i;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<bool> IsShowInProjectAsync(
        WeihnachtsfeierDbContext dbContext,
        Guid projectId,
        Guid showId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ShowProjektZuos.AnyAsync(
            link => link.ProjektId == projectId && link.ShowId == showId, cancellationToken);
    }

    internal static (Slide? Slide, string? Error) CreateSlide(SlideRequest request, Show show)
    {
        Slide? slide = request.Type?.Trim().ToLowerInvariant() switch
        {
            "poll" => new PollSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "announcement" => new AnnouncementSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "quiz" => new QuizSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "ranking" => new RankingSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "estimation" => new EstimationSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "buzzer" => new BuzzerSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "hitster" => new HitsterSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "text" => new TextSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "word_cloud" => new WordCloudSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "slider" => new SliderSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "rating" => new RatingSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "ordering" => new OrderingSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "countdown" => new CountdownSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "point_award" => new PointAwardSlide { Show = show, ShowId = show.Id, Position = request.Position },
            "jeopardy" => new JeopardySlide { Show = show, ShowId = show.Id, Position = request.Position },
            _ => null
        };
        if (slide is null)
        {
            return (null, "Unbekannter Slide-Typ.");
        }

        var result = ApplyRequest(slide, request);
        return result.Error is null ? (slide, null) : (null, result.Error);
    }

    internal static (bool Success, string? Error) ApplyRequest(Slide slide, SlideRequest request)
    {
        if (request.Position < 0)
        {
            return (false, "Die Position darf nicht negativ sein.");
        }

        try
        {
            switch (slide)
            {
                case PollSlide poll when string.Equals(request.Type, "poll", StringComparison.OrdinalIgnoreCase):
                    poll.Parameters = Deserialize<PollSlideParameters>(request.Parameters);
                    break;
                case AnnouncementSlide announcement when string.Equals(request.Type, "announcement", StringComparison.OrdinalIgnoreCase):
                    announcement.Parameters = Deserialize<AnnouncementSlideParameters>(request.Parameters);
                    break;
                case QuizSlide quiz when string.Equals(request.Type, "quiz", StringComparison.OrdinalIgnoreCase):
                    quiz.Parameters = Deserialize<QuizSlideParameters>(request.Parameters);
                    break;
                case RankingSlide ranking when string.Equals(request.Type, "ranking", StringComparison.OrdinalIgnoreCase):
                    ranking.Parameters = Deserialize<RankingSlideParameters>(request.Parameters);
                    break;
                case EstimationSlide estimation when string.Equals(request.Type, "estimation", StringComparison.OrdinalIgnoreCase):
                    estimation.Parameters = Deserialize<EstimationSlideParameters>(request.Parameters);
                    break;
                case BuzzerSlide buzzer when string.Equals(request.Type, "buzzer", StringComparison.OrdinalIgnoreCase):
                    buzzer.Parameters = Deserialize<BuzzerSlideParameters>(request.Parameters);
                    break;
                case HitsterSlide hitster when string.Equals(request.Type, "hitster", StringComparison.OrdinalIgnoreCase):
                    hitster.Parameters = Deserialize<HitsterSlideParameters>(request.Parameters);
                    if (string.IsNullOrWhiteSpace(hitster.Parameters.Title) || hitster.Parameters.Title.Trim().Length > 120)
                    {
                        return (false, "Der Hitster-Titel muss zwischen 1 und 120 Zeichen lang sein.");
                    }
                    if (hitster.Parameters.Instructions.Length > 1000 || hitster.Parameters.CardsToWin is < 3 or > 20)
                    {
                        return (false, "Die Anleitung darf maximal 1000 Zeichen lang sein und die Siegbedingung muss zwischen 3 und 20 Karten liegen.");
                    }
                    break;
                case TextSlide text when string.Equals(request.Type, "text", StringComparison.OrdinalIgnoreCase):
                    text.Parameters = Deserialize<TextSlideParameters>(request.Parameters);
                    break;
                case WordCloudSlide wc when string.Equals(request.Type, "word_cloud", StringComparison.OrdinalIgnoreCase):
                    wc.Parameters = Deserialize<WordCloudSlideParameters>(request.Parameters);
                    break;
                case SliderSlide slider when string.Equals(request.Type, "slider", StringComparison.OrdinalIgnoreCase):
                    slider.Parameters = Deserialize<SliderSlideParameters>(request.Parameters);
                    break;
                case RatingSlide rating when string.Equals(request.Type, "rating", StringComparison.OrdinalIgnoreCase):
                    rating.Parameters = Deserialize<RatingSlideParameters>(request.Parameters);
                    break;
                case OrderingSlide ordering when string.Equals(request.Type, "ordering", StringComparison.OrdinalIgnoreCase):
                    ordering.Parameters = Deserialize<OrderingSlideParameters>(request.Parameters);
                    break;
                case CountdownSlide countdown when string.Equals(request.Type, "countdown", StringComparison.OrdinalIgnoreCase):
                    countdown.Parameters = Deserialize<CountdownSlideParameters>(request.Parameters);
                    break;
                case PointAwardSlide pointAward when string.Equals(request.Type, "point_award", StringComparison.OrdinalIgnoreCase):
                    pointAward.Parameters = Deserialize<PointAwardSlideParameters>(request.Parameters);
                    break;
                case JeopardySlide jeopardy when string.Equals(request.Type, "jeopardy", StringComparison.OrdinalIgnoreCase):
                    jeopardy.Parameters = Deserialize<JeopardySlideParameters>(request.Parameters);
                    var categories = jeopardy.Parameters.Categories ?? [];
                    if (categories.Count == 0
                        || categories.Any(category => category is null
                            || string.IsNullOrWhiteSpace(category.Name)
                            || category.Clues is null))
                    {
                        return (false, "Jeopardy braucht mindestens eine Kategorie mit Namen.");
                    }
                    break;
                default:
                    return (false, "Der Slide-Typ kann beim Aktualisieren nicht geändert werden.");
            }
        }
        catch (Exception ex)
        {
            return (false, "Die Slide-Parameter sind kein gültiges JSON für diesen Typ: " + ex.Message);
        }

        return (true, null);
    }

    private static T Deserialize<T>(JsonElement parameters) where T : SlideParameters, new()
    {
        return parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new T()
            : parameters.Deserialize<T>(JsonOptions) ?? new T();
    }

    internal static SlideResponse ToResponse(Slide slide)
    {
        return new SlideResponse(
            slide.Id,
            slide.ShowId,
            GetSlideType(slide),
            slide.Position,
            JsonDocument.Parse(slide.ParametersJson).RootElement);
    }

    internal static string GetSlideType(Slide slide) => slide switch
    {
        AnnouncementSlide => "announcement",
        PollSlide => "poll",
        QuizSlide => "quiz",
        EstimationSlide => "estimation",
        BuzzerSlide => "buzzer",
        HitsterSlide => "hitster",
        RankingSlide => "ranking",
        TextSlide => "text",
        WordCloudSlide => "word_cloud",
        SliderSlide => "slider",
        RatingSlide => "rating",
        OrderingSlide => "ordering",
        CountdownSlide => "countdown",
        PointAwardSlide => "point_award",
        JeopardySlide => "jeopardy",
        _ => "announcement"
    };
}
