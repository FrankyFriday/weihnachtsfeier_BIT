using Microsoft.AspNetCore.DataProtection;
using WeihnachtsfeierBit.Framework;
using WeihnachtsfeierBit.ParticipantClient.Components;
using WeihnachtsfeierBit.ParticipantClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var keysFolder = Path.Combine(Path.GetTempPath(), "weihnachtsfeier-keys");
Directory.CreateDirectory(keysFolder);
builder.Services.AddDataProtection()
    .SetApplicationName("WeihnachtsfeierBit")
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDataProtection()
    .SetApplicationName("WeihnachtsfeierBit.ParticipantClient")
    .PersistKeysToFileSystem(new DirectoryInfo("/var/dpkeys"));

builder.Services.AddWeihnachtsfeierFramework(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice";
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped<ParticipantNotificationService>();

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
