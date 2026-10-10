using WeihnachtsfeierBit.Web;
using WeihnachtsfeierBit.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

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

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
