using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class GameInstanceEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/game-instance", GetActiveGameInstanceAsync).WithName("GetActiveGameInstance");
        admin.MapPost("/game-instance", StartGameInstanceAsync).WithName("StartGameInstance");
        admin.MapDelete("/game-instance", EndGameInstanceAsync).WithName("EndGameInstance");
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
            .FirstOrDefaultAsync(gi => gi.AccessCode == cleanCode, cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Ungültiger oder abgelaufener Session-Code."));
        }

        return Results.Ok(new ResolveCodeResponse(instance.Id, instance.ProjektId, instance.Projekt.Name, instance.AccessCode));
    }

    private static async Task<IResult> GetActiveGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.GameInstances.AsNoTracking()
            .FirstOrDefaultAsync(gi => gi.ProjektId == projectId, cancellationToken);

        if (instance is null)
        {
            return Results.NotFound(new ApiError("Keine aktive Play-Session vorhanden."));
        }

        return Results.Ok(new GameInstanceResponse(instance.Id, instance.ProjektId, instance.AccessCode, instance.CreatedAtUtc));
    }

    private static async Task<IResult> StartGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        // Remove any previous instance for this project
        var previousInstances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId)
            .ToListAsync(cancellationToken);

        var previousAccessCodes = previousInstances.Select(instance => instance.AccessCode).ToArray();
        dbContext.GameInstances.RemoveRange(previousInstances);

        var newCode = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
        var newInstance = new GameInstance
        {
            ProjektId = projectId,
            Projekt = project,
            AccessCode = newCode,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.GameInstances.Add(newInstance);

        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyCanvasSessionsEndedAsync(previousAccessCodes, cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Created($"/api/projects/{projectId}/game-instance",
            new GameInstanceResponse(newInstance.Id, newInstance.ProjektId, newInstance.AccessCode, newInstance.CreatedAtUtc));
    }

    private static async Task<IResult> EndGameInstanceAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var instances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId)
            .ToListAsync(cancellationToken);

        var accessCodes = instances.Select(instance => instance.AccessCode).ToArray();
        dbContext.GameInstances.RemoveRange(instances);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyCanvasSessionsEndedAsync(accessCodes, cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<string> GenerateUniqueCodeAsync(WeihnachtsfeierDbContext dbContext, CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = Random.Shared.Next(100000, 999999).ToString();
            var exists = await dbContext.GameInstances.AnyAsync(gi => gi.AccessCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }
}
