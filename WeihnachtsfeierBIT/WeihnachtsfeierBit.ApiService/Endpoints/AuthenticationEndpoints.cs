using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/api/projects");

        projects.MapPost("/", CreateProjectAsync)
            .RequireRateLimiting("authentication")
            .WithName("CreateProject");

        projects.MapPost("/{projectId:guid}/participant-sessions", LoginParticipantAsync)
            .RequireRateLimiting("authentication")
            .WithName("LoginParticipant");

        projects.MapPost("/{projectId:guid}/admin-sessions", LoginAdminAsync)
            .RequireRateLimiting("authentication")
            .WithName("LoginProjectAdmin");

        var reader = projects.MapGroup("/{projectId:guid}")
            .AddEndpointFilter<RequireProjectAccessFilter>();
        reader.MapDelete("/participant-sessions/current", LogoutParticipantAsync)
            .WithName("LogoutParticipant");
        reader.MapDelete("/admin-sessions/current", LogoutAdminAsync)
            .WithName("LogoutProjectAdmin");

        var admin = projects.MapGroup("/{projectId:guid}")
            .AddEndpointFilter<RequireProjectAdminFilter>();
        admin.MapGet("/admins", GetAdminsAsync).WithName("GetProjectAdmins");
        admin.MapPost("/admins", AddAdminAsync).WithName("AddProjectAdmin");
        admin.MapDelete("/admins/{adminId:guid}", RemoveAdminAsync).WithName("RemoveProjectAdmin");
        admin.MapGet("/admin-sessions", GetAdminSessionsAsync).WithName("GetAdminSessions");
        admin.MapDelete("/admin-sessions/{sessionId:guid}", RevokeAdminSessionAsync)
            .WithName("RevokeAdminSession");
        admin.MapGet("/participants/{participantId:guid}/sessions", GetParticipantSessionsAsync)
            .WithName("GetParticipantSessions");
        admin.MapDelete("/participants/{participantId:guid}/sessions/{sessionId:guid}", RevokeParticipantSessionAsync)
            .WithName("RevokeParticipantSession");

        reader.MapGet("/me/sessions", GetMySessionsAsync).WithName("GetMySessions");
        reader.MapDelete("/me/sessions/{sessionId:guid}", RevokeMySessionAsync).WithName("RevokeMySession");

        return endpoints;
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        ProjectAuthenticationService authentication,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authentication.CreateProjectAsync(
            request, httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        if (result.Error is not null)
        {
            return Results.BadRequest(new ApiError(result.Error));
        }

        return Results.Created($"/api/projects/{result.Project!.Id}", new
        {
            project = result.Project,
            session = result.Session
        });
    }

    private static async Task<IResult> LoginParticipantAsync(
        Guid projectId,
        ParticipantLoginRequest request,
        ProjectAuthenticationService authentication,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authentication.LoginParticipantAsync(
            projectId, request.Number, httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        return result.Session is null
            ? Results.Unauthorized()
            : Results.Ok(result.Session);
    }

    private static async Task<IResult> LoginAdminAsync(
        Guid projectId,
        AdminLoginRequest request,
        ProjectAuthenticationService authentication,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authentication.LoginAdminAsync(
            projectId, request, httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        return result.Session is null
            ? Results.Unauthorized()
            : Results.Ok(result.Session);
    }

    private static async Task<IResult> LogoutParticipantAsync(
        ProjectAccessService accessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (access.IsAdmin)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        await accessService.RevokeParticipantSessionAsync(
            ProjectAccessService.GetBearerToken(httpContext.Request), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAdminAsync(
        ProjectAccessService accessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsAdmin)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        await accessService.RevokeAdminSessionAsync(
            ProjectAccessService.GetBearerToken(httpContext.Request), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAdminsAsync(Guid projectId, WeihnachtsfeierDbContext dbContext, CancellationToken cancellationToken)
    {
        var admins = await dbContext.ProjektAdmins.AsNoTracking()
            .Where(admin => admin.ProjektId == projectId)
            .OrderBy(admin => admin.Benutzername)
            .Select(admin => new { admin.Id, admin.Benutzername, admin.ErstelltAmUtc })
            .ToListAsync(cancellationToken);
        return Results.Ok(admins);
    }

    private static async Task<IResult> AddAdminAsync(
        Guid projectId,
        CreateAdminRequest request,
        ProjectAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var result = await authentication.AddAdminAsync(projectId, request, cancellationToken);
        return result.Admin is null
            ? Results.BadRequest(new ApiError(result.Error!))
            : Results.Created($"/api/projects/{projectId}/admins/{result.Admin.Id}", new
            {
                result.Admin.Id,
                result.Admin.Benutzername,
                result.Admin.ErstelltAmUtc
            });
    }

    private static async Task<IResult> RemoveAdminAsync(
        Guid projectId,
        Guid adminId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var admin = await dbContext.ProjektAdmins
            .Include(item => item.Sitzungen)
            .FirstOrDefaultAsync(item => item.ProjektId == projectId && item.Id == adminId, cancellationToken);
        if (admin is null)
        {
            return Results.NotFound();
        }

        if (GetAccess(httpContext).AdminId == adminId)
        {
            return Results.Conflict(new ApiError("Du kannst dein eigenes Admin-Konto nicht entfernen."));
        }

        var adminCount = await dbContext.ProjektAdmins.CountAsync(item => item.ProjektId == projectId, cancellationToken);
        if (adminCount <= 1)
        {
            return Results.Conflict(new ApiError("Das letzte Admin-Konto eines Projekts kann nicht entfernt werden."));
        }

        dbContext.ProjektAdmins.Remove(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAdminSessionsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await dbContext.ProjektAdminSitzungen.AsNoTracking()
            .Where(session => session.ProjektAdmin.ProjektId == projectId &&
                session.WiderrufenAmUtc == null && session.LaeuftAbAmUtc > now)
            .OrderByDescending(session => session.LetzteAktivitaetAmUtc)
            .Select(session => new
            {
                session.Id,
                session.ProjektAdminId,
                Username = session.ProjektAdmin.Benutzername,
                session.ErstelltAmUtc,
                session.LetzteAktivitaetAmUtc,
                session.LaeuftAbAmUtc,
                session.UserAgent
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeAdminSessionAsync(
        Guid projectId,
        Guid sessionId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ProjektAdminSitzungen.FirstOrDefaultAsync(item =>
            item.Id == sessionId && item.ProjektAdmin.ProjektId == projectId && item.WiderrufenAmUtc == null,
            cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetParticipantSessionsAsync(
        Guid projectId,
        Guid participantId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await dbContext.Sitzungen.AsNoTracking()
            .Where(session => session.Teilnehmer.ProjektId == projectId &&
                session.TeilnehmerId == participantId &&
                session.WiderrufenAmUtc == null && session.LaeuftAbAmUtc > now)
            .OrderByDescending(session => session.LetzteAktivitaetAmUtc)
            .Select(session => new SessionSummary(session.Id, session.TeilnehmerId, session.ErstelltAmUtc,
                session.LetzteAktivitaetAmUtc, session.LaeuftAbAmUtc, session.UserAgent))
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeParticipantSessionAsync(
        Guid projectId,
        Guid participantId,
        Guid sessionId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sitzungen.FirstOrDefaultAsync(item =>
            item.Id == sessionId && item.TeilnehmerId == participantId &&
            item.Teilnehmer.ProjektId == projectId && item.WiderrufenAmUtc == null, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMySessionsAsync(
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var sessions = await dbContext.Sitzungen.AsNoTracking()
            .Where(session => session.TeilnehmerId == participantId)
            .OrderByDescending(session => session.LetzteAktivitaetAmUtc)
            .Select(session => new SessionSummary(session.Id, session.TeilnehmerId, session.ErstelltAmUtc,
                session.LetzteAktivitaetAmUtc, session.LaeuftAbAmUtc, session.UserAgent))
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeMySessionAsync(
        HttpContext httpContext,
        Guid sessionId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var session = await dbContext.Sitzungen.FirstOrDefaultAsync(item =>
            item.Id == sessionId && item.TeilnehmerId == participantId, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.WiderrufenAmUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static ProjectAccess GetAccess(HttpContext httpContext) =>
        (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
}