using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class AdminSharingEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin");

        group.MapGet("/admins/search", SearchAdminsAsync).WithName("SearchAdmins");

        // Project admins
        group.MapGet("/projects/{projectId:guid}/admins", GetProjectAdminsAsync).WithName("GetProjectShareAdmins");
        group.MapPost("/projects/{projectId:guid}/admins", InviteProjectAdminAsync).WithName("InviteProjectAdmin");
        group.MapDelete("/projects/{projectId:guid}/admins/{adminId:guid}", RemoveProjectAdminAsync).WithName("RemoveProjectShareAdmin");

        // Show admins
        group.MapGet("/shows/{showId:guid}/admins", GetShowAdminsAsync).WithName("GetShowAdmins");
        group.MapPost("/shows/{showId:guid}/admins", InviteShowAdminAsync).WithName("InviteShowAdmin");
        group.MapDelete("/shows/{showId:guid}/admins/{adminId:guid}", RemoveShowAdminAsync).WithName("RemoveShowAdmin");
    }

    private static async Task<IResult> SearchAdminsAsync(
        string? q,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var query = q?.Trim().ToLowerInvariant() ?? string.Empty;
        var admins = await dbContext.ProjektAdmins.AsNoTracking()
            .Where(a => string.IsNullOrEmpty(query) || a.BenutzernameNormalisiert.Contains(query))
            .OrderBy(a => a.Benutzername)
            .Take(10)
            .Select(a => new AdminDto(a.Id, a.Benutzername))
            .ToListAsync(cancellationToken);

        return Results.Ok(admins);
    }

    private static async Task<IResult> GetProjectAdminsAsync(
        Guid projectId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var project = await dbContext.Projekte.AsNoTracking()
            .Include(p => p.Admins)
                .ThenInclude(a => a.ProjektAdmin)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null) return Results.NotFound();

        bool isMember = project.ErstellerAdminId == adminId || project.Admins.Any(a => a.ProjektAdminId == adminId);
        if (!isMember) return Results.Forbid();

        var list = project.Admins.Select(a => new AdminShareItemDto(
            a.ProjektAdminId,
            a.ProjektAdmin.Benutzername,
            project.ErstellerAdminId == a.ProjektAdminId,
            a.HinzugefuegtAmUtc)).ToList();

        return Results.Ok(new AdminShareListResponse(project.ErstellerAdminId == adminId, list));
    }

    private static async Task<IResult> InviteProjectAdminAsync(
        Guid projectId,
        InviteAdminRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var project = await dbContext.Projekte
            .Include(p => p.Admins)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null) return Results.NotFound();

        if (project.ErstellerAdminId.HasValue && project.ErstellerAdminId != adminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller des Projekts kann andere Admins einladen."));
        }

        var targetUsername = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            return Results.BadRequest(new ApiError("Benutzername ist erforderlich."));
        }

        var targetAdmin = await dbContext.ProjektAdmins
            .FirstOrDefaultAsync(a => a.BenutzernameNormalisiert == targetUsername.ToUpperInvariant(), cancellationToken);

        if (targetAdmin is null)
        {
            return Results.NotFound(new ApiError("Admin nicht gefunden."));
        }

        if (project.Admins.Any(a => a.ProjektAdminId == targetAdmin.Id))
        {
            return Results.BadRequest(new ApiError("Dieser Admin hat bereits Zugriff auf das Projekt."));
        }

        dbContext.ProjektAdminProjekte.Add(new ProjektAdminProjekt
        {
            ProjektAdminId = targetAdmin.Id,
            ProjektId = projectId,
            HinzugefuegtAmUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> RemoveProjectAdminAsync(
        Guid projectId,
        Guid adminIdToRemove,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var project = await dbContext.Projekte
            .Include(p => p.Admins)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null) return Results.NotFound();

        if (project.ErstellerAdminId != adminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller des Projekts kann Einladungen entfernen."));
        }

        if (project.ErstellerAdminId == adminIdToRemove)
        {
            return Results.BadRequest(new ApiError("Der Ersteller kann nicht entfernt werden."));
        }

        var membership = project.Admins.FirstOrDefault(a => a.ProjektAdminId == adminIdToRemove);
        if (membership is not null)
        {
            dbContext.ProjektAdminProjekte.Remove(membership);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetShowAdminsAsync(
        Guid showId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var show = await dbContext.Shows.AsNoTracking()
            .Include(s => s.Admins)
                .ThenInclude(a => a.ProjektAdmin)
            .FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);

        if (show is null) return Results.NotFound();

        bool isMember = show.ErstellerAdminId == adminId || show.Admins.Any(a => a.ProjektAdminId == adminId);
        if (!isMember) return Results.Forbid();

        var list = show.Admins.Select(a => new AdminShareItemDto(
            a.ProjektAdminId,
            a.ProjektAdmin.Benutzername,
            show.ErstellerAdminId == a.ProjektAdminId,
            a.HinzugefuegtAmUtc)).ToList();

        return Results.Ok(new AdminShareListResponse(show.ErstellerAdminId == adminId, list));
    }

    private static async Task<IResult> InviteShowAdminAsync(
        Guid showId,
        InviteAdminRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var show = await dbContext.Shows
            .Include(s => s.Admins)
            .FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);

        if (show is null) return Results.NotFound();

        if (show.ErstellerAdminId.HasValue && show.ErstellerAdminId != adminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller der Show kann andere Admins einladen."));
        }

        var targetUsername = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            return Results.BadRequest(new ApiError("Benutzername ist erforderlich."));
        }

        var targetAdmin = await dbContext.ProjektAdmins
            .FirstOrDefaultAsync(a => a.BenutzernameNormalisiert == targetUsername.ToUpperInvariant(), cancellationToken);

        if (targetAdmin is null)
        {
            return Results.NotFound(new ApiError("Admin nicht gefunden."));
        }

        if (show.Admins.Any(a => a.ProjektAdminId == targetAdmin.Id))
        {
            return Results.BadRequest(new ApiError("Dieser Admin hat bereits Zugriff auf die Show."));
        }

        dbContext.ProjektAdminShows.Add(new ProjektAdminShow
        {
            ProjektAdminId = targetAdmin.Id,
            ShowId = showId,
            HinzugefuegtAmUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> RemoveShowAdminAsync(
        Guid showId,
        Guid adminIdToRemove,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null) return Results.Unauthorized();

        var show = await dbContext.Shows
            .Include(s => s.Admins)
            .FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);

        if (show is null) return Results.NotFound();

        if (show.ErstellerAdminId != adminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller der Show kann Einladungen entfernen."));
        }

        if (show.ErstellerAdminId == adminIdToRemove)
        {
            return Results.BadRequest(new ApiError("Der Ersteller kann nicht entfernt werden."));
        }

        var membership = show.Admins.FirstOrDefault(a => a.ProjektAdminId == adminIdToRemove);
        if (membership is not null)
        {
            dbContext.ProjektAdminShows.Remove(membership);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }
}

public sealed record AdminDto(Guid Id, string Username);
public sealed record InviteAdminRequest(string Username);
public sealed record AdminShareItemDto(Guid AdminId, string Username, bool IsCreator, DateTime AddedAtUtc);
public sealed record AdminShareListResponse(bool CanManage, List<AdminShareItemDto> Admins);
