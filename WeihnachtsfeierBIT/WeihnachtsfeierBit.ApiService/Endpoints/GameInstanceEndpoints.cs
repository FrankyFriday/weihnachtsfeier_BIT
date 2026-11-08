using Microsoft.AspNetCore.Mvc;
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
        reader.MapGet("/game-instance", GetActiveGameInstanceAsync).WithName("GetActiveGameInstance");
        admin.MapPost("/game-instance", StartGameInstanceAsync).WithName("StartGameInstance");
        admin.MapDelete("/game-instance", EndGameInstanceAsync).WithName("EndGameInstance");
    }

    public static void MapPublic(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/game-instances/by-code/{code}", ResolveCodeAsync)
            .RequireRateLimiting("public-session-read");
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
        [FromBody] StartGameInstanceRequest? request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Play-Session zu starten."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        string newCode;
        var preferredCode = request?.PreferredAccessCode?.Trim();
        if (!string.IsNullOrWhiteSpace(preferredCode))
        {
            if (preferredCode.Length != 6 || !preferredCode.All(char.IsDigit))
            {
                return Results.BadRequest(new ApiError("Der Wunsch-Code muss aus genau 6 Ziffern bestehen (z. B. 123456)."));
            }

            var isCodeOccupied = await dbContext.GameInstances.AnyAsync(
                gi => gi.AccessCode == preferredCode && gi.ProjektId != projectId,
                cancellationToken);

            if (isCodeOccupied)
            {
                return Results.Conflict(new ApiError($"Der Wunsch-Code '{preferredCode}' ist aktuell bereits durch eine andere aktive Session belegt."));
            }

            newCode = preferredCode;
        }
        else
        {
            newCode = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
        }

        // Remove any previous instance for this project
        var previousInstances = await dbContext.GameInstances
            .Where(gi => gi.ProjektId == projectId)
            .ToListAsync(cancellationToken);

        var previousAccessCodes = previousInstances.Select(instance => instance.AccessCode).ToArray();
        dbContext.GameInstances.RemoveRange(previousInstances);

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
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanControlSession)
        {
            return Results.BadRequest(new ApiError("Du hast keine Berechtigung, die Play-Session zu beenden."));
        }

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
