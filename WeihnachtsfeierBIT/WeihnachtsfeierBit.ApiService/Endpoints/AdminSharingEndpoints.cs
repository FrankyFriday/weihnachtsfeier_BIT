using Microsoft.AspNetCore.Mvc;
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

        // Project admins & permissions
        group.MapGet("/projects/{projectId:guid}/admins", GetProjectAdminsAsync).WithName("GetProjectShareAdmins");
        group.MapPost("/projects/{projectId:guid}/admins", InviteProjectAdminAsync).WithName("InviteProjectAdmin");
        group.MapPut("/projects/{projectId:guid}/admins/{adminId:guid}/permissions", UpdateProjectAdminPermissionsAsync).WithName("UpdateProjectAdminPermissions");
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
        var currentAdminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (currentAdminId is null)
        {
            return Results.Unauthorized();
        }

        var query = q?.Trim().ToUpperInvariant() ?? string.Empty;
        var admins = await dbContext.ProjektAdmins.AsNoTracking()
            .Where(a => a.Id != currentAdminId && (string.IsNullOrEmpty(query) || a.BenutzernameNormalisiert.Contains(query)))
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

        var project = await dbContext.Projekte
            .Include(p => p.Admins)
                .ThenInclude(a => a.ProjektAdmin)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null) return Results.NotFound();

        bool isMember = project.ErstellerAdminId == adminId || project.Admins.Any(a => a.ProjektAdminId == adminId);
        if (!isMember) return Results.Forbid();

        bool canManage = project.ErstellerAdminId == adminId;

        var list = project.Admins.Select(a => new AdminShareItemDto(
            a.ProjektAdminId,
            a.ProjektAdmin.Benutzername,
            project.ErstellerAdminId == a.ProjektAdminId,
            a.HinzugefuegtAmUtc,
            a.CanControlSession,
            a.CanManageParticipants,
            a.CanEditProjectSettings,
            a.CanManageShows)).ToList();

        return Results.Ok(new AdminShareListResponse(canManage, list));
    }

    private static async Task<IResult> InviteProjectAdminAsync(
        Guid projectId,
        [FromBody] InviteAdminRequest request,
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
            return Results.Forbid();
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
            HinzugefuegtAmUtc = DateTime.UtcNow,
            CanControlSession = true,
            CanManageParticipants = true,
            CanEditProjectSettings = false,
            CanManageShows = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateProjectAdminPermissionsAsync(
        Guid projectId,
        Guid adminId,
        [FromBody] UpdateProjectAdminPermissionsRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentAdminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (currentAdminId is null) return Results.Unauthorized();

        var project = await dbContext.Projekte
            .Include(p => p.Admins)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null) return Results.NotFound();

        if (project.ErstellerAdminId != currentAdminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller des Projekts kann Berechtigungen anpassen."));
        }

        var membership = await dbContext.ProjektAdminProjekte
            .FirstOrDefaultAsync(a => a.ProjektId == projectId && a.ProjektAdminId == adminId, cancellationToken);

        if (membership is null)
        {
            return Results.NotFound(new ApiError("Mitgliedschaft nicht gefunden."));
        }

        membership.CanControlSession = request.CanControlSession;
        membership.CanManageParticipants = request.CanManageParticipants;
        membership.CanEditProjectSettings = request.CanEditProjectSettings;
        membership.CanManageShows = request.CanManageShows;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> RemoveProjectAdminAsync(
        Guid projectId,
        Guid adminId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentAdminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (currentAdminId is null) return Results.Unauthorized();

        var project = await dbContext.Projekte.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null) return Results.NotFound();

        if (project.ErstellerAdminId != currentAdminId)
        {
            return Results.Forbid();
        }

        if (project.ErstellerAdminId == adminId)
        {
            return Results.BadRequest(new ApiError("Der Ersteller kann nicht entfernt werden."));
        }

        var membership = await dbContext.ProjektAdminProjekte
            .FirstOrDefaultAsync(m => m.ProjektId == projectId && m.ProjektAdminId == adminId, cancellationToken);

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

        var show = await dbContext.Shows
            .Include(s => s.Admins)
                .ThenInclude(a => a.ProjektAdmin)
            .FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);

        if (show is null) return Results.NotFound();

        // Auto-assign creator for legacy unassigned show
        if (!show.ErstellerAdminId.HasValue)
        {
            show.ErstellerAdminId = adminId;
            if (!show.Admins.Any(sa => sa.ProjektAdminId == adminId.Value))
            {
                dbContext.ProjektAdminShows.Add(new ProjektAdminShow { ProjektAdminId = adminId.Value, ShowId = showId });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        bool isMember = show.ErstellerAdminId == adminId || show.Admins.Any(a => a.ProjektAdminId == adminId);
        if (!isMember) return Results.Forbid();

        bool canManage = show.ErstellerAdminId == adminId;

        var list = show.Admins.Select(a => new AdminShareItemDto(
            a.ProjektAdminId,
            a.ProjektAdmin.Benutzername,
            show.ErstellerAdminId == a.ProjektAdminId,
            a.HinzugefuegtAmUtc,
            CanControlSession: true,
            CanManageParticipants: true,
            CanEditProjectSettings: true,
            CanManageShows: true)).ToList();

        return Results.Ok(new AdminShareListResponse(canManage, list));
    }

    private static async Task<IResult> InviteShowAdminAsync(
        Guid showId,
        [FromBody] InviteAdminRequest request,
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

        if (!show.ErstellerAdminId.HasValue)
        {
            show.ErstellerAdminId = adminId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (show.ErstellerAdminId != adminId)
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
        Guid adminId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentAdminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (currentAdminId is null) return Results.Unauthorized();

        var show = await dbContext.Shows.FirstOrDefaultAsync(s => s.Id == showId, cancellationToken);
        if (show is null) return Results.NotFound();

        if (!show.ErstellerAdminId.HasValue)
        {
            show.ErstellerAdminId = currentAdminId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (show.ErstellerAdminId != currentAdminId)
        {
            return Results.BadRequest(new ApiError("Nur der Ersteller der Show kann Einladungen entfernen."));
        }

        if (show.ErstellerAdminId == adminId)
        {
            return Results.BadRequest(new ApiError("Der Ersteller kann nicht entfernt werden."));
        }

        var membership = await dbContext.ProjektAdminShows
            .FirstOrDefaultAsync(m => m.ShowId == showId && m.ProjektAdminId == adminId, cancellationToken);

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
public sealed record UpdateProjectAdminPermissionsRequest(
    bool CanControlSession,
    bool CanManageParticipants,
    bool CanEditProjectSettings,
    bool CanManageShows);
public sealed record AdminShareItemDto(
    Guid AdminId,
    string Username,
    bool IsCreator,
    DateTime AddedAtUtc,
    bool CanControlSession = true,
    bool CanManageParticipants = true,
    bool CanEditProjectSettings = false,
    bool CanManageShows = false);
public sealed record AdminShareListResponse(bool CanManage, List<AdminShareItemDto> Admins);
