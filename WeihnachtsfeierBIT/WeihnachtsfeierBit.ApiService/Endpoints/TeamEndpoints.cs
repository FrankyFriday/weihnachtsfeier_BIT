using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class TeamEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapPost("/teams", CreateTeamAsync).WithName("CreateTeam");
        admin.MapPut("/teams/{teamId:guid}", UpdateTeamAsync).WithName("UpdateTeam");
        admin.MapDelete("/teams/{teamId:guid}", DeleteTeamAsync).WithName("DeleteTeam");
    }

    private static async Task<IResult> CreateTeamAsync(
        Guid projectId,
        CreateTeamRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim();
        var color = request.Color?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 ||
            string.IsNullOrWhiteSpace(color) || color.Length > 32)
        {
            return Results.BadRequest(new ApiError("Teamname (max. 80) und Farbe (max. 32) sind erforderlich."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var team = new Team
        {
            DisplayName = displayName,
            Color = color,
            ProjektId = projectId,
            Projekt = project
        };
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Created($"/api/projects/{projectId}/teams/{team.Id}",
            new TeamResponse(team.Id, team.DisplayName, team.Color));
    }

    private static async Task<IResult> UpdateTeamAsync(
        Guid projectId,
        Guid teamId,
        UpdateTeamRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim();
        var color = request.Color?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 ||
            string.IsNullOrWhiteSpace(color) || color.Length > 32)
        {
            return Results.BadRequest(new ApiError("Teamname oder Farbe ist ungültig."));
        }

        var team = await dbContext.Teams.FirstOrDefaultAsync(
            item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        team.DisplayName = displayName;
        team.Color = color;
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(new TeamResponse(team.Id, team.DisplayName, team.Color));
    }

    private static async Task<IResult> DeleteTeamAsync(
        Guid projectId,
        Guid teamId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(
            item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        if (await dbContext.Teilnehmer.AnyAsync(item => item.TeamId == teamId, cancellationToken))
        {
            return Results.Conflict(new ApiError("Das Team enthält noch Teilnehmer. Weise sie zuerst einem anderen Team zu."));
        }

        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.NoContent();
    }
}
