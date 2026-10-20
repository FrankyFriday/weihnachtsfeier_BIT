using WeihnachtsfeierBit.Framework;
using WeihnachtsfeierBit.ParticipantClient.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddWeihnachtsfeierFramework(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice";
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

app.UsePathBase("/play");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
