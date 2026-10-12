using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ProjectShowEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/shows", GetShowsAsync).WithName("GetProjectShows");
        admin.MapPost("/shows", LinkShowAsync).WithName("LinkShowToProject");
        admin.MapDelete("/shows/{showId:guid}", UnlinkShowAsync).WithName("UnlinkShowFromProject");
    }

    private static async Task<IResult> GetShowsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var shows = await dbContext.ShowProjektZuos.AsNoTracking()
            .Where(link => link.ProjektId == projectId)
            .OrderBy(link => link.Show.Name)
            .Select(link => new ShowResponse(link.ShowId, link.Show.Name))
            .ToListAsync(cancellationToken);
        return Results.Ok(shows);
    }

    private static async Task<IResult> LinkShowAsync(
        Guid projectId,
        LinkShowToProjectRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var show = await dbContext.Shows.FindAsync([request.ShowId], cancellationToken);
        if (show is null)
        {
            return Results.NotFound();
        }

        if (await dbContext.ShowProjektZuos.AnyAsync(
                link => link.ProjektId == projectId && link.ShowId == request.ShowId,
                cancellationToken))
        {
            return Results.Conflict(new ApiError("Diese Show ist bereits mit dem Projekt verknüpft."));
        }

        dbContext.ShowProjektZuos.Add(new ShowProjektZuo
        {
            Show = show,
            ShowId = show.Id,
            Projekt = project,
            ProjektId = projectId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/projects/{projectId}/shows/{show.Id}", new ShowResponse(show.Id, show.Name));
    }

    private static async Task<IResult> UnlinkShowAsync(
        Guid projectId,
        Guid showId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.ShowProjektZuos.FirstOrDefaultAsync(
            item => item.ProjektId == projectId && item.ShowId == showId,
            cancellationToken);
        if (link is null)
        {
            return Results.NotFound();
        }

        dbContext.ShowProjektZuos.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}