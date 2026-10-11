using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class TestEntryEndpoints
{
    public static IEndpointRouteBuilder MapTestEntryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/test-entries").WithTags("Development test data");
        group.MapGet("", async (WeihnachtsfeierDbContext dbContext, CancellationToken cancellationToken) =>
            await dbContext.TestEntries.AsNoTracking().ToListAsync(cancellationToken))
            .WithName("GetTestEntries");

        group.MapPost("", CreateAsync).WithName("CreateTestEntry");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateTestEntryRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Results.BadRequest(new ApiError("Name muss zwischen 1 und 200 Zeichen lang sein."));
        }

        var entry = new TestEntry { Name = name };
        dbContext.TestEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/test-entries/{entry.Id}", entry);
    }
}