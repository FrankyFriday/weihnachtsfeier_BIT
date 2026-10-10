using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<WeihnachtsfeierDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("weihnachtsfeierdb")));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/test-entries", async (WeihnachtsfeierDbContext dbContext) =>
    await dbContext.TestEntries.AsNoTracking().ToListAsync())
    .WithName("GetTestEntries");

app.MapPost("/test-entries", async (CreateTestEntryRequest request, WeihnachtsfeierDbContext dbContext) =>
{
    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new { message = "Name darf nicht leer sein." });
    }

    var entry = new TestEntry { Name = name };
    dbContext.TestEntries.Add(entry);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/test-entries/{entry.Id}", entry);
})
    .WithName("CreateTestEntry");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record CreateTestEntryRequest(string? Name);
