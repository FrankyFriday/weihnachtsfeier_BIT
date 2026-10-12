using Microsoft.AspNetCore.Authentication.Cookies;
using WeihnachtsfeierBit.AdminClient;
using WeihnachtsfeierBit.AdminClient.Components;
using WeihnachtsfeierBit.AdminClient.Endpoints;
using WeihnachtsfeierBit.Framework;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ProjectShowLinksState>();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Weihnachtsfeier.Admin";
        options.Cookie.Path = "/admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = false;
    });

builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice";
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddWeihnachtsfeierFramework(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https+http://apiservice";
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

app.UsePathBase("/admin");
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapAdminAuthenticationEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();