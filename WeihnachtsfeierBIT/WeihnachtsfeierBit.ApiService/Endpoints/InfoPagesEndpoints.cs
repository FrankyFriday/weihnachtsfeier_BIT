using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class InfoPagesEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/info-pages", GetInfoPagesAsync).WithName("GetInfoPages");
        reader.MapGet("/info-pages/{id:guid}", GetInfoPageByIdAsync).WithName("GetInfoPageById");

        admin.MapPost("/info-pages", CreateInfoPageAsync).WithName("CreateInfoPage");
        admin.MapPut("/info-pages/{id:guid}", UpdateInfoPageAsync).WithName("UpdateInfoPage");
        admin.MapDelete("/info-pages/{id:guid}", DeleteInfoPageAsync).WithName("DeleteInfoPage");
    }

    private static async Task<IResult> GetInfoPagesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);

        var query = dbContext.ProjectInfoPages.AsNoTracking()
            .Where(p => p.ProjektId == projectId);

        if (access?.AdminId == null)
        {
            query = query.Where(p => p.IsVisible);
        }

        var pages = await query.OrderBy(p => p.Position).ThenBy(p => p.Title)
            .Select(page => new ProjectInfoPageResponse(
                page.Id,
                page.ProjektId,
                page.Title,
                page.Icon,
                page.Position,
                page.IsVisible,
                page.ContentMarkdown,
                page.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(pages);
    }

    private static async Task<IResult> GetInfoPageByIdAsync(
        Guid projectId,
        Guid id,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var page = await dbContext.ProjectInfoPages.AsNoTracking()
            .Where(p => p.Id == id && p.ProjektId == projectId)
            .Select(p => new ProjectInfoPageResponse(
                p.Id,
                p.ProjektId,
                p.Title,
                p.Icon,
                p.Position,
                p.IsVisible,
                p.ContentMarkdown,
                p.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (page is null) return Results.NotFound();
        return Results.Ok(page);
    }

    private static async Task<IResult> CreateInfoPageAsync(
        Guid projectId,
        CreateInfoPageRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var pageId = request.Id is { } requestId && requestId != Guid.Empty
            ? requestId
            : Guid.NewGuid();
        var existingPage = await dbContext.ProjectInfoPages.AsNoTracking()
            .Where(page => page.Id == pageId && page.ProjektId == projectId)
            .Select(page => new ProjectInfoPageResponse(
                page.Id,
                page.ProjektId,
                page.Title,
                page.Icon,
                page.Position,
                page.IsVisible,
                page.ContentMarkdown,
                page.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPage is not null)
        {
            var requestedIcon = string.IsNullOrWhiteSpace(request.Icon) ? "info" : request.Icon.Trim();
            var requestedContent = request.ContentMarkdown ?? string.Empty;
            return existingPage.Title == request.Title.Trim() &&
                existingPage.Icon == requestedIcon &&
                existingPage.Position == request.Position &&
                existingPage.IsVisible == request.IsVisible &&
                existingPage.ContentMarkdown == requestedContent
                ? Results.Ok(existingPage)
                : Results.Conflict();
        }

        var page = new ProjectInfoPage
        {
            Id = pageId,
            ProjektId = projectId,
            Title = request.Title.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "info" : request.Icon.Trim(),
            Position = request.Position,
            IsVisible = request.IsVisible,
            ContentMarkdown = request.ContentMarkdown ?? string.Empty
        };

        dbContext.ProjectInfoPages.Add(page);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(page));
    }

    private static async Task<IResult> UpdateInfoPageAsync(
        Guid projectId,
        Guid id,
        UpdateInfoPageRequest request,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var page = await dbContext.ProjectInfoPages
            .FirstOrDefaultAsync(p => p.Id == id && p.ProjektId == projectId, cancellationToken);

        if (page is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Title)) page.Title = request.Title.Trim();
        if (!string.IsNullOrWhiteSpace(request.Icon)) page.Icon = request.Icon.Trim();
        if (request.Position.HasValue) page.Position = request.Position.Value;
        if (request.IsVisible.HasValue) page.IsVisible = request.IsVisible.Value;
        if (request.ContentMarkdown is not null) page.ContentMarkdown = request.ContentMarkdown;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(page));
    }

    private static async Task<IResult> DeleteInfoPageAsync(
        Guid projectId,
        Guid id,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var page = await dbContext.ProjectInfoPages
            .FirstOrDefaultAsync(p => p.Id == id && p.ProjektId == projectId, cancellationToken);

        if (page is null) return Results.NotFound();

        dbContext.ProjectInfoPages.Remove(page);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok();
    }

    private static ProjectInfoPageResponse ToResponse(ProjectInfoPage page) =>
        new(page.Id, page.ProjektId, page.Title, page.Icon, page.Position, page.IsVisible, page.ContentMarkdown, page.CreatedAtUtc);
}

public sealed record CreateInfoPageRequest(string Title, string? Icon, int Position, bool IsVisible, string? ContentMarkdown, Guid? Id = null);
public sealed record UpdateInfoPageRequest(string? Title, string? Icon, int? Position, bool? IsVisible, string? ContentMarkdown);
public sealed record ProjectInfoPageResponse(Guid Id, Guid ProjektId, string Title, string Icon, int Position, bool IsVisible, string ContentMarkdown, DateTime CreatedAtUtc);
