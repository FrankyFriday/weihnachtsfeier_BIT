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
        admin.MapPost("/shows/{showId:guid}/slides", CreateSlideAsync).WithName("CreateSlide");
        admin.MapPut("/shows/{showId:guid}/slide-positions", ReorderSlidesAsync).WithName("ReorderSlides");
        admin.MapPut("/shows/{showId:guid}/slides/{slideId:guid}", UpdateSlideAsync).WithName("UpdateSlide");
        admin.MapDelete("/shows/{showId:guid}/slides/{slideId:guid}", DeleteSlideAsync).WithName("DeleteSlide");
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
        var link = await dbContext.ShowProjektZuos
            .Include(item => item.Show)
            .FirstOrDefaultAsync(item => item.ProjektId == projectId && item.ShowId == showId, cancellationToken);
        if (link is null)
        {
            return Results.NotFound();
        }

        var slideResult = CreateSlide(request, link.Show);
        if (slideResult.Slide is null)
        {
            return Results.BadRequest(new ApiError(slideResult.Error!));
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
            .Where(slide => slide.ShowId == showId)
            .ToListAsync(cancellationToken);

        var slideDict = slides.ToDictionary(s => s.Id);

        for (int i = 0; i < request.SlideIds.Length; i++)
        {
            if (slideDict.TryGetValue(request.SlideIds[i], out var slide))
            {
                slide.Position = i;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
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
            _ => null
        };
        if (slide is null)
        {
            return (null, "Slide-Typ muss poll, announcement, quiz, ranking, estimation oder buzzer sein.");
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
        var (type, parameters) = slide switch
        {
            PollSlide poll => ("poll", (object)poll.Parameters),
            AnnouncementSlide announcement => ("announcement", announcement.Parameters),
            QuizSlide quiz => ("quiz", quiz.Parameters),
            RankingSlide ranking => ("ranking", ranking.Parameters),
            EstimationSlide estimation => ("estimation", estimation.Parameters),
            BuzzerSlide buzzer => ("buzzer", buzzer.Parameters),
            _ => throw new InvalidOperationException("Unbekannter Slide-Typ.")
        };
        return new SlideResponse(slide.Id, slide.ShowId, type, slide.Position,
            JsonSerializer.SerializeToElement(parameters, parameters.GetType(), JsonOptions));
    }
}