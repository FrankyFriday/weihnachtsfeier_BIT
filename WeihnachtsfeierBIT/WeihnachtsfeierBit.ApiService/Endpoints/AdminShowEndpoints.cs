using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class AdminShowEndpoints
{
    public static IEndpointRouteBuilder MapAdminShowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var shows = endpoints.MapGroup("/api/admin/shows");
        shows.MapGet("", GetShowsAsync).WithName("GetAdminShows");
        shows.MapPost("", CreateShowAsync).WithName("CreateAdminShow");
        shows.MapGet("/{showId:guid}", GetShowAsync).WithName("GetAdminShow");
        shows.MapPut("/{showId:guid}", UpdateShowAsync).WithName("UpdateAdminShow");
        shows.MapDelete("/{showId:guid}", DeleteShowAsync).WithName("DeleteAdminShow");
        shows.MapPost("/{showId:guid}/duplicate", DuplicateShowAsync).WithName("DuplicateAdminShow");
        AdminShowSlideEndpoints.Map(shows);
        return endpoints;
    }

    private static async Task<IResult> GetShowsAsync(
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var shows = await dbContext.Shows.AsNoTracking()
            .OrderBy(show => show.Name)
            .Select(show => new AdminShowResponse(
                show.Id,
                show.Name,
                dbContext.ShowProjektZuos.Count(link => link.ShowId == show.Id)))
            .ToListAsync(cancellationToken);
        return Results.Ok(shows);
    }

    private static async Task<IResult> CreateShowAsync(
        CreateShowRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest(new ApiError("Der Showname muss zwischen 1 und 120 Zeichen lang sein."));
        }

        var show = new Show { Name = name };
        dbContext.Shows.Add(show);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/shows/{show.Id}", new AdminShowResponse(show.Id, show.Name, 0));
    }

    private static async Task<IResult> GetShowAsync(
        Guid showId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var show = await dbContext.Shows.AsNoTracking()
            .Where(item => item.Id == showId)
            .Select(item => new AdminShowResponse(
                item.Id,
                item.Name,
                dbContext.ShowProjektZuos.Count(link => link.ShowId == item.Id)))
            .FirstOrDefaultAsync(cancellationToken);
        return show is null ? Results.NotFound() : Results.Ok(show);
    }

    private static async Task<IResult> UpdateShowAsync(
        Guid showId,
        CreateShowRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var show = await dbContext.Shows.FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);
        if (show is null)
        {
            return Results.NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest(new ApiError("Der Showname muss zwischen 1 und 120 Zeichen lang sein."));
        }

        show.Name = name;
        await dbContext.SaveChangesAsync(cancellationToken);

        var projectCount = await dbContext.ShowProjektZuos.CountAsync(link => link.ShowId == showId, cancellationToken);
        return Results.Ok(new AdminShowResponse(show.Id, show.Name, projectCount));
    }

    private static async Task<IResult> DeleteShowAsync(
        Guid showId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var show = await dbContext.Shows.FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);
        if (show is null)
        {
            return Results.NotFound();
        }

        dbContext.Shows.Remove(show);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DuplicateShowAsync(
        Guid showId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var originalShow = await dbContext.Shows
            .Include(s => s.Slides)
            .FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);

        if (originalShow is null)
        {
            return Results.NotFound();
        }

        var newShow = new Show
        {
            Name = $"{originalShow.Name} (Kopie)"
        };

        dbContext.Shows.Add(newShow);

        foreach (var originalSlide in originalShow.Slides.OrderBy(s => s.Position))
        {
            var slideResult = ShowSlideEndpoints.CreateSlide(
                new SlideRequest(
                    originalSlide switch
                    {
                        PollSlide => "poll",
                        AnnouncementSlide => "announcement",
                        QuizSlide => "quiz",
                        RankingSlide => "ranking",
                        EstimationSlide => "estimation",
                        BuzzerSlide => "buzzer",
                        _ => "announcement"
                    },
                    originalSlide.Position,
                    JsonDocument.Parse(originalSlide.ParametersJson).RootElement),
                newShow);

            if (slideResult.Slide is not null)
            {
                dbContext.Slides.Add(slideResult.Slide);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/shows/{newShow.Id}", new AdminShowResponse(newShow.Id, newShow.Name, 0));
    }
}
