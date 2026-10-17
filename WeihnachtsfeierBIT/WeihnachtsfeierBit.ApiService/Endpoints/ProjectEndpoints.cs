using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reader = endpoints.MapGroup("/api/projects/{projectId:guid}")
            .AddEndpointFilter<RequireProjectAccessFilter>();
        var admin = endpoints.MapGroup("/api/projects/{projectId:guid}")
            .AddEndpointFilter<RequireProjectAdminFilter>();

        reader.MapGet("", GetProjectAsync).WithName("GetProject");
        reader.MapGet("/overview", GetProjectOverviewAsync).WithName("GetProjectOverview");
        admin.MapPut("", UpdateProjectAsync).WithName("UpdateProject");
        admin.MapDelete("", DeleteProjectAsync).WithName("DeleteProject");

        reader.MapGet("/participants/me", GetCurrentParticipantAsync).WithName("GetCurrentParticipant");
        reader.MapPut("/participants/me", UpdateCurrentParticipantNameAsync).WithName("UpdateCurrentParticipantName");
        reader.MapGet("/teams", GetTeamsAsync).WithName("GetTeams");
        reader.MapGet("/shows/{showId:guid}/slides", ShowSlideEndpoints.GetSlidesAsync).WithName("GetSlides");

        ParticipantEndpoints.Map(admin);
        TeamEndpoints.Map(admin);
        ProjectShowEndpoints.Map(reader, admin);
        ShowSlideEndpoints.Map(admin);
        GameInstanceEndpoints.Map(reader, admin);

        return endpoints;
    }

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new ProjectResponse(item.Id, item.Name))
            .FirstOrDefaultAsync(cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    private static async Task<IResult> GetProjectOverviewAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var overview = await dbContext.Projekte.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new ProjectOverviewResponse(
                project.Id,
                project.Name,
                dbContext.ShowProjektZuos.Count(link => link.ProjektId == project.Id),
                dbContext.Teilnehmer.Count(participant => participant.ProjektId == project.Id),
                dbContext.Teams.Count(team => team.ProjektId == project.Id)))
            .FirstOrDefaultAsync(cancellationToken);
        return overview is null ? Results.NotFound() : Results.Ok(overview);
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        RenameProjectRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Der Projektname muss zwischen 1 und 120 Zeichen lang sein."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        project.Name = name;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ProjectResponse(project.Id, project.Name));
    }

    private static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        dbContext.Projekte.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentParticipantAsync(
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var participant = await dbContext.Teilnehmer.AsNoTracking()
            .Where(item => item.Id == participantId && item.ProjektId == access.ProjectId)
            .Select(item => new ParticipantResponse(item.Id, item.Nummer, item.Name, item.Team == null ? null : item.Team.Id))
            .FirstOrDefaultAsync(cancellationToken);
        return participant is null ? Results.NotFound() : Results.Ok(participant);
    }

    private static async Task<IResult> UpdateCurrentParticipantNameAsync(
        HttpContext httpContext,
        WeihnachtsfeierBit.ApiService.Contracts.UpdateParticipantRequest request,
        WeihnachtsfeierDbContext dbContext,
        WeihnachtsfeierBit.ApiService.Services.IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (request.Name?.Length > 120)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Der Name darf höchstens 120 Zeichen lang sein."));
        }

        var participant = await dbContext.Teilnehmer.FirstOrDefaultAsync(
            item => item.Id == participantId && item.ProjektId == access.ProjectId, cancellationToken);
        if (participant is null)
        {
            return Results.NotFound();
        }

        participant.Name = request.Name?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(access.ProjectId, cancellationToken);
        return Results.Ok(new ParticipantResponse(participant.Id, participant.Nummer, participant.Name, participant.TeamId));
    }

    private static async Task<IResult> GetTeamsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.Teams.AsNoTracking()
            .Where(team => team.ProjektId == projectId)
            .OrderBy(team => team.DisplayName)
            .Select(team => new TeamResponse(team.Id, team.DisplayName, team.Color))
            .ToListAsync(cancellationToken);
        return Results.Ok(teams);
    }
}