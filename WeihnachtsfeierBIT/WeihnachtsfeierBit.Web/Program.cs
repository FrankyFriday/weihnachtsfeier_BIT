using WeihnachtsfeierBit.Web;
using WeihnachtsfeierBit.Web.Components;
using WeihnachtsfeierBit.Framework;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
builder.Services.AddWeihnachtsfeierFramework(client =>
{
    client.BaseAddress = new(builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice");
});

var adminClientAddress = builder.Configuration["AdminClientBaseUrl"]
    ?? builder.Configuration["services:adminclient:http:0"]
    ?? "http://adminclient:8080";
var participantClientAddress = builder.Configuration["ParticipantClientBaseUrl"]
    ?? builder.Configuration["services:participantclient:http:0"]
    ?? "http://participantclient:8080";

builder.Services.AddReverseProxy().LoadFromMemory(
[
    new RouteConfig
    {
        RouteId = "admin-client",
        ClusterId = "admin-client",
        Match = new RouteMatch { Path = "/admin/{**catch-all}" },
        Transforms =
        [
            new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }
        ]
    },
    new RouteConfig
    {
        RouteId = "participant-client",
        ClusterId = "participant-client",
        Match = new RouteMatch { Path = "/play/{**catch-all}" },
        Transforms =
        [
            new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }
        ]
    }
],
[
    new ClusterConfig
    {
        ClusterId = "admin-client",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["admin-client"] = new() { Address = adminClientAddress }
        }
    },
    new ClusterConfig
    {
        ClusterId = "participant-client",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["participant-client"] = new() { Address = participantClientAddress }
        }
    }
]);

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddHttpClient<TestEntryApiClient>(client =>
{
    client.BaseAddress = new(builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice");
});

var app = builder.Build();

app.MapPost("/test-entry-form", async (HttpRequest request, TestEntryApiClient apiClient, CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken);
    var name = form["name"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Bitte einen Namen eingeben.");
    }

    await apiClient.CreateEntryAsync(name, cancellationToken);
    return Results.Redirect("/");
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();
app.MapReverseProxy();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
