using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
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
        reader.MapGet("/permissions/global", GetGlobalPermissionsAsync).WithName("GetGlobalPermissions");
        admin.MapPut("", UpdateProjectAsync).WithName("UpdateProject");
        admin.MapPut("/permissions/global", UpdateGlobalPermissionsAsync).WithName("UpdateGlobalPermissions");
        admin.MapDelete("", DeleteProjectAsync).WithName("DeleteProject");

        reader.MapGet("/participants/me", GetCurrentParticipantAsync).WithName("GetCurrentParticipant");
        reader.MapPut("/participants/me", UpdateCurrentParticipantNameAsync).WithName("UpdateCurrentParticipantName");
        reader.MapGet("/teams", GetTeamsAsync).WithName("GetTeams");
        reader.MapGet("/shows/{showId:guid}/slides", ShowSlideEndpoints.GetSlidesAsync).WithName("GetSlides");

        ParticipantEndpoints.Map(admin);
        TeamEndpoints.Map(reader, admin);
        ProjectShowEndpoints.Map(reader, admin);
        ShowSlideEndpoints.Map(admin);
        GameInstanceEndpoints.Map(reader, admin);
        SessionControlEndpoints.Map(reader, admin);
        ChatEndpoints.Map(reader, admin);
        InfoPagesEndpoints.Map(reader, admin);
        PartyFeaturesEndpoints.Map(reader, admin);

        return endpoints;
    }

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new WeihnachtsfeierBit.ApiService.Contracts.ProjectResponse(item.Id, item.Name))
            .FirstOrDefaultAsync(cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    private static async Task<IResult> GetProjectOverviewAsync(
        Guid projectId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        var overview = await dbContext.Projekte.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new WeihnachtsfeierBit.ApiService.Contracts.ProjectOverviewResponse(
                project.Id,
                project.Name,
                dbContext.ShowProjektZuos.Count(link => link.ProjektId == project.Id),
                dbContext.Teilnehmer.Count(participant => participant.ProjektId == project.Id),
                dbContext.Teams.Count(team => team.ProjektId == project.Id),
                new WeihnachtsfeierBit.ApiService.Contracts.ProjectPermissionsDto(
                    access.IsCreator,
                    access.CanControlSession,
                    access.CanManageParticipants,
                    access.CanEditProjectSettings,
                    access.CanManageShows)))
            .FirstOrDefaultAsync(cancellationToken);
        return overview is null ? Results.NotFound() : Results.Ok(overview);
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        WeihnachtsfeierBit.ApiService.Contracts.RenameProjectRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanEditProjectSettings)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Du hast keine Berechtigung, die Einstellungen dieses Projekts zu bearbeiten."));
        }

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
        return Results.Ok(new WeihnachtsfeierBit.ApiService.Contracts.ProjectResponse(project.Id, project.Name));
    }

    private static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.IsCreator)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Nur der Ersteller kann das Projekt löschen."));
        }

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
            .Include(item => item.Berechtigung)
            .FirstOrDefaultAsync(item => item.Id == participantId && item.ProjektId == access.ProjectId, cancellationToken);
        if (participant is null) return Results.NotFound();

        var projectPermissions = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == access.ProjectId)
            .Select(item => item.PermissionsJson)
            .FirstOrDefaultAsync(cancellationToken);
        var permissions = ParticipantPermissionKeys.ResolveEffectivePermissions(
            projectPermissions,
            participant.Berechtigung?.PermissionsJson);
        return Results.Ok(new ParticipantResponse(
            participant.Id,
            participant.Nummer,
            participant.Name,
            participant.TeamId,
            permissions));
    }

    private static async Task<IResult> UpdateCurrentParticipantNameAsync(
        HttpContext httpContext,
        WeihnachtsfeierBit.ApiService.Contracts.UpdateParticipantRequest request,
        WeihnachtsfeierDbContext dbContext,
        ParticipantPermissionService permissionService,
        WeihnachtsfeierBit.ApiService.Services.IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (!await permissionService.AllowsAsync(
                access,
                ParticipantPermissionKeys.CanChangeName,
                requireInteraction: true,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.Name?.Length > 120)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Der Name darf höchstens 120 Zeichen lang sein."));
        }

        var participant = await dbContext.Teilnehmer
            .Include(item => item.Berechtigung)
            .FirstOrDefaultAsync(
            item => item.Id == participantId && item.ProjektId == access.ProjectId, cancellationToken);
        if (participant is null)
        {
            return Results.NotFound();
        }

        participant.Name = request.Name?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(access.ProjectId, cancellationToken);
        var projectPermissions = await dbContext.Projekte.AsNoTracking()
            .Where(item => item.Id == access.ProjectId)
            .Select(item => item.PermissionsJson)
            .FirstOrDefaultAsync(cancellationToken);
        var permissions = ParticipantPermissionKeys.ResolveEffectivePermissions(
            projectPermissions,
            participant.Berechtigung?.PermissionsJson);
        return Results.Ok(new ParticipantResponse(
            participant.Id,
            participant.Nummer,
            participant.Name,
            participant.TeamId,
            permissions));
    }

    private static async Task<IResult> GetTeamsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.Teams.AsNoTracking()
            .Where(team => team.ProjektId == projectId)
            .OrderBy(team => team.DisplayName)
            .Select(team => new TeamResponse(team.Id, team.DisplayName, team.Color, team.Punkte))
            .ToListAsync(cancellationToken);
        return Results.Ok(teams);
    }

    private static async Task<IResult> GetGlobalPermissionsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null) return Results.NotFound();

        var perms = ParticipantPermissionKeys.ParseNullablePermissionsJson(project.PermissionsJson);
        var dict = new Dictionary<string, bool?>();
        foreach (var key in ParticipantPermissionKeys.Metadata.Keys)
        {
            dict[key] = perms.TryGetValue(key, out var val) ? val : null;
        }

        return Results.Ok(dict);
    }

    private static async Task<IResult> UpdateGlobalPermissionsAsync(
        Guid projectId,
        UpdateProjectGlobalPermissionsRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        ChatHubAccessSynchronizer chatHubAccessSynchronizer,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (!access.CanEditProjectSettings)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Du hast keine Berechtigung, die Projekteinstellungen zu bearbeiten."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null) return Results.NotFound();

        project.PermissionsJson = JsonSerializer.Serialize(request.Permissions);
        await dbContext.SaveChangesAsync(cancellationToken);
        await chatHubAccessSynchronizer.RefreshProjectAsync(projectId, cancellationToken);
        _ = gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);

        return Results.Ok(request.Permissions);
    }
}