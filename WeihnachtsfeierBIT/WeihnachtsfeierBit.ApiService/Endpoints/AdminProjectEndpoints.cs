using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class AdminProjectEndpoints
{
    public static IEndpointRouteBuilder MapAdminProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/api/admin/projects");
        projects.MapGet("", GetProjectsAsync).WithName("GetAdminProjects");
        projects.MapPost("", CreateProjectAsync).WithName("CreateAdminProject");
        return endpoints;
    }

    private static async Task<IResult> GetProjectsAsync(
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null)
        {
            return Results.Unauthorized();
        }

        var projects = await dbContext.ProjektAdminProjekte.AsNoTracking()
            .Where(membership => membership.ProjektAdminId == adminId)
            .OrderBy(membership => membership.Projekt.Name)
            .Select(membership => new ProjectSummary(membership.Projekt.Id, membership.Projekt.Name))
            .ToListAsync(cancellationToken);
        return Results.Ok(projects);
    }

    private static async Task<IResult> CreateProjectAsync(
        WeihnachtsfeierBit.ApiService.Contracts.CreateProjectRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var adminId = await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken);
        if (adminId is null)
        {
            return Results.Unauthorized();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest(new WeihnachtsfeierBit.ApiService.Contracts.ApiError("Der Projektname muss zwischen 1 und 120 Zeichen lang sein."));
        }

        var project = new Projekt { Name = name };
        dbContext.ProjektAdminProjekte.Add(new ProjektAdminProjekt
        {
            ProjektAdminId = adminId.Value,
            Projekt = project
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/projects/{project.Id}", new ProjectSummary(project.Id, project.Name));
    }

}