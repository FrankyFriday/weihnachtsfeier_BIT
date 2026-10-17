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
        admin.MapPost("/game-instances/draft", CreateDraftGameInstanceAsync).WithName("CreateDraftGameInstance");
        admin.MapPost("/game-instances", StartGameInstanceAsync).WithName("StartGameInstance");
        admin.MapPost("/game-instances/{instanceId:guid}/start", StartGameInstanceByIdAsync).WithName("StartGameInstanceById");
        admin.MapPost("/game-instances/{instanceId:guid}/end", EndGameInstanceAsync).WithName("EndGameInstance");
        admin.MapPost("/game-instances/{instanceId:guid}/resume", ResumeGameInstanceAsync).WithName("ResumeGameInstance");
        admin.MapPut("/game-instances/{instanceId:guid}/teams/{teamId:guid}/points", UpdateTeamInstancePointsAsync).WithName("UpdateTeamInstancePoints");
        admin.MapGet("/game-instances/{instanceId:guid}/participants", GetGameInstanceParticipantsAsync).WithName("GetGameInstanceParticipants");
        admin.MapPut("/game-instances/{instanceId:guid}/participants/{participantId:guid}/team", UpdateSessionParticipantTeamAsync).WithName("UpdateSessionParticipantTeam");
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
            .Include(gi => gi.TeamInstances)
            .ThenInclude(ti => ti.Team)
            .FirstOrDefaultAsync(cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Play-Session wurde nicht gefunden."));
        }

        var teamResponses = instance.TeamInstances
            .Select(ti => new TeamInstanceResponse(ti.TeamId, ti.Team.DisplayName, ti.Team.Color, ti.Punkte))
            .ToArray();

        return Results.Ok(new GameInstanceDetailResponse(
            instance.Id,
            instance.ProjektId,
            instance.AccessCode,
            instance.IsActive,
            instance.CreatedAtUtc,
            instance.EndedAtUtc,
            teamResponses));
    }

    private static async Task<IResult> GetActiveGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances.AsNoTracking()
            .Where(gi => gi.ProjektId == projectId && gi.IsActive)
            .Include(gi => gi.TeamInstances)
            .ThenInclude(ti => ti.Team)
            .FirstOrDefaultAsync(cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        var teamResponses = instance.TeamInstances
            .Select(ti => new TeamInstanceResponse(ti.TeamId, ti.Team.DisplayName, ti.Team.Color, ti.Punkte))
            .ToArray();

        return Results.Ok(new GameInstanceDetailResponse(
            instance.Id,
            instance.ProjektId,
            instance.AccessCode,
            instance.IsActive,
            instance.CreatedAtUtc,
            instance.EndedAtUtc,
            teamResponses));
    }

    private static async Task<IResult> CreateDraftGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        var newInstance = new GameInstance
        {
            ProjektId = projectId,
            Projekt = project,
            AccessCode = null,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.GameInstances.Add(newInstance);

        // Populate TeamInstances for all existing teams
        var teams = await dbContext.Teams.Where(t => t.ProjektId == projectId).ToListAsync(cancellationToken);
        foreach (var team in teams)
        {
            dbContext.TeamInstances.Add(new TeamInstance
            {
                GameInstance = newInstance,
                Team = team,
                Punkte = 0
            });
        }

        // Populate TeilnehmerInstances for all existing participants
        var participants = await dbContext.Teilnehmer.Where(p => p.ProjektId == projectId).ToListAsync(cancellationToken);
        foreach (var p in participants)
        {
            dbContext.TeilnehmerInstances.Add(new TeilnehmerInstance
            {
                GameInstance = newInstance,
                Teilnehmer = p,
                TeamId = p.TeamId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/projects/{projectId}/game-instances/{newInstance.Id}",
            new GameInstanceResponse(newInstance.Id, newInstance.ProjektId, newInstance.AccessCode, newInstance.IsActive, newInstance.CreatedAtUtc, newInstance.EndedAtUtc));
    }

    private static async Task<IResult> StartGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        // Deactivate any currently active instance for this project
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
            dbContext.TeamInstances.Add(new TeamInstance
            {
                GameInstance = newInstance,
                Team = team,
                Punkte = 0
            });
        }

        var participants = await dbContext.Teilnehmer.Where(p => p.ProjektId == projectId).ToListAsync(cancellationToken);
        foreach (var p in participants)
        {
            dbContext.TeilnehmerInstances.Add(new TeilnehmerInstance
            {
                GameInstance = newInstance,
                Teilnehmer = p,
                TeamId = p.TeamId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Created($"/api/projects/{projectId}/game-instances/{newInstance.Id}",
            new GameInstanceResponse(newInstance.Id, newInstance.ProjektId, newInstance.AccessCode, newInstance.IsActive, newInstance.CreatedAtUtc, newInstance.EndedAtUtc));
    }

    private static async Task<IResult> StartGameInstanceByIdAsync(
        Guid projectId,
        Guid instanceId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);

        if (instance is null) return Results.NotFound();

        // Deactivate any other active instance
        var activeInstances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId && gi.IsActive && gi.Id != instanceId)
            .ToListAsync(cancellationToken);

        foreach (var active in activeInstances)
        {
            active.IsActive = false;
            active.AccessCode = null;
            active.EndedAtUtc = DateTime.UtcNow;
        }

        var newCode = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
        instance.IsActive = true;
        instance.AccessCode = newCode;
        instance.StartedAtUtc = DateTime.UtcNow;
        instance.EndedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new GameInstanceResponse(instance.Id, instance.ProjektId, instance.AccessCode, instance.IsActive, instance.CreatedAtUtc, instance.EndedAtUtc));
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

    private static async Task<IResult> ResumeGameInstanceAsync(
        Guid projectId,
        Guid instanceId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances
            .FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);

        if (instance is null) return Results.NotFound();

        var activeInstances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId && gi.IsActive && gi.Id != instanceId)
            .ToListAsync(cancellationToken);

        foreach (var active in activeInstances)
        {
            active.IsActive = false;
            active.AccessCode = null;
            active.EndedAtUtc = DateTime.UtcNow;
        }

        var newCode = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
        instance.IsActive = true;
        instance.AccessCode = newCode;
        instance.EndedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new GameInstanceResponse(instance.Id, instance.ProjektId, instance.AccessCode, instance.IsActive, instance.CreatedAtUtc, instance.EndedAtUtc));
    }

    private static async Task<IResult> UpdateTeamInstancePointsAsync(
        Guid projectId,
        Guid instanceId,
        Guid teamId,
        UpdateTeamInstancePointsRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        if (request.Points < 0)
        {
            return Results.BadRequest(new ApiError("Der Punktestand darf nicht negativ sein."));
        }

        var teamInstance = await dbContext.TeamInstances
            .Include(ti => ti.Team)
            .FirstOrDefaultAsync(ti => ti.GameInstanceId == instanceId && ti.TeamId == teamId && ti.GameInstance.ProjektId == projectId, cancellationToken);

        if (teamInstance is null)
        {
            var instance = await dbContext.GameInstances.FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);
            var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.ProjektId == projectId, cancellationToken);
            if (instance is null || team is null) return Results.NotFound();

            teamInstance = new TeamInstance
            {
                GameInstance = instance,
                Team = team,
                Punkte = request.Points
            };
            dbContext.TeamInstances.Add(teamInstance);
        }
        else
        {
            teamInstance.Punkte = request.Points;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(new TeamInstanceResponse(teamInstance.TeamId, teamInstance.Team.DisplayName, teamInstance.Team.Color, teamInstance.Punkte));
    }

    private static async Task<IResult> GetGameInstanceParticipantsAsync(
        Guid projectId,
        Guid instanceId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participants = await dbContext.TeilnehmerInstances.AsNoTracking()
            .Where(tni => tni.GameInstanceId == instanceId && tni.GameInstance.ProjektId == projectId)
            .OrderBy(tni => tni.Teilnehmer.Nummer)
            .Select(tni => new TeilnehmerInstanceResponse(tni.Id, tni.GameInstanceId, tni.TeilnehmerId, tni.Teilnehmer.Nummer, tni.Teilnehmer.Name, tni.TeamId))
            .ToListAsync(cancellationToken);

        return Results.Ok(participants);
    }

    private static async Task<IResult> UpdateSessionParticipantTeamAsync(
        Guid projectId,
        Guid instanceId,
        Guid participantId,
        UpdateSessionParticipantTeamRequest request,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances.FirstOrDefaultAsync(gi => gi.Id == instanceId && gi.ProjektId == projectId, cancellationToken);
        if (instance is null) return Results.NotFound();

        var participantInstance = await dbContext.TeilnehmerInstances
            .FirstOrDefaultAsync(tni => tni.GameInstanceId == instanceId && tni.TeilnehmerId == participantId, cancellationToken);

        if (participantInstance is null)
        {
            var p = await dbContext.Teilnehmer.FirstOrDefaultAsync(item => item.Id == participantId && item.ProjektId == projectId, cancellationToken);
            if (p is null) return Results.NotFound();

            participantInstance = new TeilnehmerInstance
            {
                GameInstance = instance,
                Teilnehmer = p,
                TeamId = request.TeamId
            };
            dbContext.TeilnehmerInstances.Add(participantInstance);
        }
        else
        {
            participantInstance.TeamId = request.TeamId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok();
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
