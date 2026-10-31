using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class TeamEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/point-logs", GetPointLogsAsync).WithName("GetPointLogs");

        admin.MapPost("/teams", CreateTeamAsync).WithName("CreateTeam");
        admin.MapPut("/teams/{teamId:guid}", UpdateTeamAsync).WithName("UpdateTeam");
        admin.MapPut("/teams/{teamId:guid}/points", UpdateTeamPointsAsync).WithName("UpdateTeamPoints");
        admin.MapPost("/teams/{teamId:guid}/award-points", AwardTeamPointsAsync).WithName("AwardTeamPoints");
        admin.MapPut("/point-logs/{logId:guid}", UpdatePointLogAsync).WithName("UpdatePointLog");
        admin.MapDelete("/point-logs/{logId:guid}", DeletePointLogAsync).WithName("DeletePointLog");
        admin.MapDelete("/teams/{teamId:guid}", DeleteTeamAsync).WithName("DeleteTeam");
    }

    private static async Task<IResult> GetPointLogsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var logs = await dbContext.TeamPointLogs.AsNoTracking()
            .Where(pl => pl.ProjektId == projectId)
            .Include(pl => pl.Team)
            .OrderByDescending(pl => pl.CreatedAtUtc)
            .Select(pl => new TeamPointLogDto(
                pl.Id,
                pl.TeamId,
                pl.Team.DisplayName,
                pl.Team.Color,
                pl.DeltaPoints,
                pl.NewTotalPoints,
                pl.Reason,
                pl.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(logs);
    }

    private static async Task<IResult> AwardTeamPointsAsync(
        Guid projectId,
        Guid teamId,
        AwardTeamPointsRequest request,
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

        int delta = request.DeltaPoints;
        int oldPoints = team.Punkte;
        int newPoints = Math.Max(0, oldPoints + delta);
        team.Punkte = newPoints;

        string reason = string.IsNullOrWhiteSpace(request.Reason) ? (delta >= 0 ? "Punktbonifikation" : "Punktabzug") : request.Reason.Trim();

        var log = new TeamPointLog
        {
            ProjektId = projectId,
            TeamId = teamId,
            DeltaPoints = delta,
            NewTotalPoints = newPoints,
            Reason = reason,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.TeamPointLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new TeamResponse(team.Id, team.DisplayName, team.Color, team.Punkte));
    }

    private static async Task<IResult> UpdatePointLogAsync(
        Guid projectId,
        Guid logId,
        UpdatePointLogRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var targetLog = await dbContext.TeamPointLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.ProjektId == projectId, cancellationToken);

        if (targetLog is null) return Results.NotFound();

        targetLog.DeltaPoints = request.DeltaPoints;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            targetLog.Reason = request.Reason.Trim();
        }

        await RecalculateTeamPointsAsync(targetLog.TeamId, dbContext, cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> DeletePointLogAsync(
        Guid projectId,
        Guid logId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var targetLog = await dbContext.TeamPointLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.ProjektId == projectId, cancellationToken);

        if (targetLog is null) return Results.NotFound();

        Guid teamId = targetLog.TeamId;
        dbContext.TeamPointLogs.Remove(targetLog);

        await RecalculateTeamPointsAsync(teamId, dbContext, cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task RecalculateTeamPointsAsync(
        Guid teamId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null) return;

        var teamLogs = await dbContext.TeamPointLogs
            .Where(l => l.TeamId == teamId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        int runningTotal = 0;
        foreach (var log in teamLogs)
        {
            runningTotal = Math.Max(0, runningTotal + log.DeltaPoints);
            log.NewTotalPoints = runningTotal;
        }

        team.Punkte = runningTotal;
        await dbContext.SaveChangesAsync(cancellationToken);
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
            Punkte = 0,
            ProjektId = projectId,
            Projekt = project
        };
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Created($"/api/projects/{projectId}/teams/{team.Id}",
            new TeamResponse(team.Id, team.DisplayName, team.Color, team.Punkte));
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
            string.IsNullOrWhiteSpace(color) || color.Length > 32 || request.Points < 0)
        {
            return Results.BadRequest(new ApiError("Teamname, Farbe oder Punktzahl ist ungültig."));
        }

        var team = await dbContext.Teams.FirstOrDefaultAsync(
            item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        team.DisplayName = displayName;
        team.Color = color;
        team.Punkte = request.Points;
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(new TeamResponse(team.Id, team.DisplayName, team.Color, team.Punkte));
    }

    private static async Task<IResult> UpdateTeamPointsAsync(
        Guid projectId,
        Guid teamId,
        UpdateTeamPointsRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (request.Points < 0)
        {
            return Results.BadRequest(new ApiError("Der Punktestand darf nicht negativ sein."));
        }

        var team = await dbContext.Teams.FirstOrDefaultAsync(
            item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        int delta = request.Points - team.Punkte;
        team.Punkte = request.Points;

        var log = new TeamPointLog
        {
            ProjektId = projectId,
            TeamId = teamId,
            DeltaPoints = delta,
            NewTotalPoints = request.Points,
            Reason = "Manuelle Anpassung",
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.TeamPointLogs.Add(log);

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(new TeamResponse(team.Id, team.DisplayName, team.Color, team.Punkte));
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

public sealed record AwardTeamPointsRequest(int DeltaPoints, string? Reason);
public sealed record UpdatePointLogRequest(int DeltaPoints, string? Reason);
public sealed record TeamPointLogDto(Guid Id, Guid TeamId, string TeamName, string TeamColor, int DeltaPoints, int NewTotalPoints, string Reason, DateTime CreatedAtUtc);
