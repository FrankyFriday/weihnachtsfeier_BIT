using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Endpoints;
using WeihnachtsfeierBit.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<WeihnachtsfeierDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("weihnachtsfeierdb")));

builder.Services.AddSignalR();
builder.Services.AddSingleton<WeihnachtsfeierBit.ApiService.Services.IGameStateNotifier, WeihnachtsfeierBit.ApiService.Services.GameStateNotifier>();

builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<ProjectAccessService>();
builder.Services.AddScoped<AdminSessionAccessService>();
builder.Services.AddScoped<ProjectAuthenticationService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

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
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapHub<WeihnachtsfeierBit.ApiService.Hubs.GameStateHub>("/hubs/gamestate");
GameInstanceEndpoints.MapPublic(app);
app.MapAuthenticationEndpoints();
app.MapProjectEndpoints();
app.MapWeatherEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapTestEntryEndpoints();
}

app.MapDefaultEndpoints();

app.Run();

