using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class GameInstanceEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/game-instances/active", GetActiveGameInstanceAsync).WithName("GetActiveGameInstance");
        admin.MapGet("/game-instances", GetGameInstancesAsync).WithName("GetGameInstances");
        admin.MapGet("/game-instances/{instanceId:guid}", GetGameInstanceDetailAsync).WithName("GetGameInstanceDetail");
        admin.MapPost("/game-instances", StartGameInstanceAsync).WithName("StartGameInstance");
        admin.MapPost("/game-instances/{instanceId:guid}/end", EndGameInstanceAsync).WithName("EndGameInstance");
        admin.MapPut("/game-instances/{instanceId:guid}/teams/{teamId:guid}/score", UpdateTeamScoreAsync).WithName("UpdateTeamScore");
    }

    public static void MapPublic(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/game-instances/by-code/{code}", ResolveCodeAsync)
            .RequireRateLimiting("authentication");
    }

    private static async Task<IResult> ResolveCodeAsync(
        string code,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var cleanCode = code.Trim();
        if (cleanCode.Length != 6)
        {
            return Results.BadRequest(new ApiError("Der Session-Code muss 6-stellig sein."));
        }

        var instance = await dbContext.GameInstances.AsNoTracking()
            .Include(gi => gi.Projekt)
            .FirstOrDefaultAsync(gi => gi.IsActive && gi.AccessCode == cleanCode, cancellationToken);

        if (instance is null || instance.AccessCode is null)
        {
            return Results.NotFound(new ApiError("Ungültiger oder abgelaufener Session-Code."));
        }

        return Results.Ok(new ResolveCodeResponse(instance.Id, instance.ProjektId, instance.Projekt.Name, instance.AccessCode));
    }

    private static async Task<IResult> GetGameInstancesAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instances = await dbContext.GameInstances.AsNoTracking()
            .Where(gi => gi.ProjektId == projectId)
            .OrderByDescending(gi => gi.CreatedAtUtc)
            .Select(gi => new GameInstanceResponse(gi.Id, gi.ProjektId, gi.AccessCode, gi.IsActive, gi.CreatedAtUtc, gi.EndedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(instances);
    }

    private static async Task<IResult> GetGameInstanceDetailAsync(
        Guid projectId,
        Guid instanceId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances.AsNoTracking()
            .Where(gi => gi.ProjektId == projectId && gi.Id == instanceId)
            .Include(gi => gi.TeamScores)
            .ThenInclude(ts => ts.Team)
            .FirstOrDefaultAsync(cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Play-Session wurde nicht gefunden."));
        }

        var projectTeams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ProjektId == projectId)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        var scoreMap = instance.TeamScores.ToDictionary(ts => ts.TeamId, ts => ts.Points);

        var teamScores = projectTeams.Select(t => new TeamScoreResponse(
            t.Id,
            t.DisplayName,
            t.Color,
            scoreMap.GetValueOrDefault(t.Id, 0)
        )).ToArray();

        return Results.Ok(new GameInstanceDetailResponse(
            instance.Id,
            instance.ProjektId,
            instance.AccessCode,
            instance.IsActive,
            instance.CreatedAtUtc,
            instance.EndedAtUtc,
            teamScores));
    }

    private static async Task<IResult> GetActiveGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances.AsNoTracking()
            .Where(gi => gi.ProjektId == projectId && gi.IsActive)
            .Include(gi => gi.TeamScores)
            .ThenInclude(ts => ts.Team)
            .FirstOrDefaultAsync(cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        var projectTeams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ProjektId == projectId)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        var scoreMap = instance.TeamScores.ToDictionary(ts => ts.TeamId, ts => ts.Points);

        var teamScores = projectTeams.Select(t => new TeamScoreResponse(
            t.Id,
            t.DisplayName,
            t.Color,
            scoreMap.GetValueOrDefault(t.Id, 0)
        )).ToArray();

        return Results.Ok(new GameInstanceDetailResponse(
            instance.Id,
            instance.ProjektId,
            instance.AccessCode,
            instance.IsActive,
            instance.CreatedAtUtc,
            instance.EndedAtUtc,
            teamScores));
    }

    private static async Task<IResult> StartGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        // Deactivate active instances
        var activeInstances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId && gi.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var active in activeInstances)
        {
            active.IsActive = false;
            active.AccessCode = null;
            active.EndedAtUtc = DateTime.UtcNow;
        }

        var newCode = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
        var newInstance = new GameInstance
        {
            ProjektId = projectId,
            Projekt = project,
            AccessCode = newCode,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = DateTime.UtcNow
        };
        dbContext.GameInstances.Add(newInstance);

        var teams = await dbContext.Teams.Where(t => t.ProjektId == projectId).ToListAsync(cancellationToken);
        foreach (var team in teams)
        {
            dbContext.TeamScores.Add(new TeamScore
            {
                GameInstance = newInstance,
                Team = team,
                Points = 0
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Created($"/api/projects/{projectId}/game-instances/{newInstance.Id}",
            new GameInstanceResponse(newInstance.Id, newInstance.ProjektId, newInstance.AccessCode, newInstance.IsActive, newInstance.CreatedAtUtc, newInstance.EndedAtUtc));
    }

    private static async Task<IResult> EndGameInstanceAsync(
        Guid projectId,
        Guid instanceId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);

        if (instance is null) return Results.NotFound();

        instance.IsActive = false;
        instance.AccessCode = null;
        instance.EndedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new GameInstanceResponse(instance.Id, instance.ProjektId, instance.AccessCode, instance.IsActive, instance.CreatedAtUtc, instance.EndedAtUtc));
    }

    private static async Task<IResult> UpdateTeamScoreAsync(
        Guid projectId,
        Guid instanceId,
        Guid teamId,
        UpdateTeamScoreRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (request.Points < 0)
        {
            return Results.BadRequest(new ApiError("Der Punktestand darf nicht negativ sein."));
        }

        var teamScore = await dbContext.TeamScores
            .Include(ts => ts.Team)
            .FirstOrDefaultAsync(ts => ts.GameInstanceId == instanceId && ts.TeamId == teamId && ts.GameInstance.ProjektId == projectId, cancellationToken);

        if (teamScore is null)
        {
            var instance = await dbContext.GameInstances.FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);
            var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.ProjektId == projectId, cancellationToken);
            if (instance is null || team is null) return Results.NotFound();

            teamScore = new TeamScore
            {
                GameInstance = instance,
                Team = team,
                Points = request.Points
            };
            dbContext.TeamScores.Add(teamScore);
        }
        else
        {
            teamScore.Points = request.Points;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new TeamScoreResponse(teamScore.TeamId, teamScore.Team.DisplayName, teamScore.Team.Color, teamScore.Points));
    }

    private static async Task<string> GenerateUniqueCodeAsync(WeihnachtsfeierDbContext dbContext, CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = Random.Shared.Next(100000, 999999).ToString();
            var exists = await dbContext.GameInstances.AnyAsync(gi => gi.IsActive && gi.AccessCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }
}
