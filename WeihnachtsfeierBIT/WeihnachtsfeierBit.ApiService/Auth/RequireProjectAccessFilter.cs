namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class RequireProjectAccessFilter : IEndpointFilter
{
    public const string AccessItemKey = "ProjectAccess";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue("projectId", out var rawProjectId) ||
            !Guid.TryParse(rawProjectId?.ToString(), out var projectId))
        {
            return Results.BadRequest();
        }

        var accessService = context.HttpContext.RequestServices.GetRequiredService<ProjectAccessService>();
        var token = ProjectAccessService.GetBearerToken(context.HttpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, context.HttpContext.RequestAborted);
        if (access is null)
        {
            return Results.Unauthorized();
        }

        context.HttpContext.Items[AccessItemKey] = access;
        return await next(context);
    }
}