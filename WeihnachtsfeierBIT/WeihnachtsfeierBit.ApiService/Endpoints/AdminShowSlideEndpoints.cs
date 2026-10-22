using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class AdminShowSlideEndpoints
{
    public static void Map(RouteGroupBuilder shows)
    {
        shows.MapGet("/{showId:guid}/slides", GetSlidesAsync).WithName("GetAdminShowSlides");
        shows.MapPost("/{showId:guid}/slides", CreateSlideAsync).WithName("CreateAdminShowSlide");
        shows.MapPut("/{showId:guid}/slides/{slideId:guid}", UpdateSlideAsync).WithName("UpdateAdminShowSlide");
        shows.MapDelete("/{showId:guid}/slides/{slideId:guid}", DeleteSlideAsync).WithName("DeleteAdminShowSlide");
    }

    private static async Task<IResult> GetSlidesAsync(
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

        if (!await dbContext.Shows.AnyAsync(show => show.Id == showId, cancellationToken))
        {
            return Results.NotFound();
        }

        var slides = await dbContext.Slides.AsNoTracking()
            .Where(slide => slide.ShowId == showId)
            .OrderBy(slide => slide.Position)
            .ToListAsync(cancellationToken);
        return Results.Ok(slides.Select(ShowSlideEndpoints.ToResponse));
    }

    private static async Task<IResult> CreateSlideAsync(
        Guid showId,
        SlideRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var show = await dbContext.Shows.FirstOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return Results.NotFound();
        }

        var result = ShowSlideEndpoints.CreateSlide(request, show);
        if (result.Slide is null)
        {
            return Results.BadRequest(new ApiError(result.Error!));
        }

        dbContext.Slides.Add(result.Slide);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/shows/{showId}/slides/{result.Slide.Id}",
            ShowSlideEndpoints.ToResponse(result.Slide));
    }

    private static async Task<IResult> UpdateSlideAsync(
        Guid showId,
        Guid slideId,
        SlideRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var slide = await dbContext.Slides.FirstOrDefaultAsync(
            item => item.Id == slideId && item.ShowId == showId, cancellationToken);
        if (slide is null)
        {
            return Results.NotFound();
        }

        var result = ShowSlideEndpoints.ApplyRequest(slide, request);
        if (result.Error is not null)
        {
            return Results.BadRequest(new ApiError(result.Error));
        }

        slide.Position = request.Position;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ShowSlideEndpoints.ToResponse(slide));
    }

    private static async Task<IResult> DeleteSlideAsync(
        Guid showId,
        Guid slideId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
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
}
