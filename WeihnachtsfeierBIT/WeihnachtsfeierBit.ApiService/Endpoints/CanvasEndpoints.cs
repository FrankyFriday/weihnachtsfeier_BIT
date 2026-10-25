using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class CanvasEndpoints
{
    public static void MapPublic(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/game-instances/by-code/{code}/canvas", GetCanvasStateAsync)
            .RequireRateLimiting("authentication")
            .WithName("GetCanvasState");
    }

    private static async Task<IResult> GetCanvasStateAsync(
        string code,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var state = await CanvasStateBuilder.BuildAsync(code.Trim(), dbContext, cancellationToken);
        return state is null
            ? Results.NotFound()
            : Results.Ok(state);
    }
}
