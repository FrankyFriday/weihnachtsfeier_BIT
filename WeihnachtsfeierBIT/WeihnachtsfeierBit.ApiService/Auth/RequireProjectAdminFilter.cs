namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed class RequireProjectAdminFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var accessService = context.HttpContext.RequestServices.GetRequiredService<ProjectAccessService>();
        var projectId = Guid.Parse(context.HttpContext.Request.RouteValues["projectId"]!.ToString()!);
        var token = ProjectAccessService.GetBearerToken(context.HttpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, context.HttpContext.RequestAborted);
        if (access is null)
        {
            return Results.Unauthorized();
        }

        if (!access.IsAdmin)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        context.HttpContext.Items[RequireProjectAccessFilter.AccessItemKey] = access;
        return await next(context);
    }
}