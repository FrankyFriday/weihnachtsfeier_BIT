using Microsoft.Extensions.DependencyInjection;
using WeihnachtsfeierBit.Framework.Services;

namespace WeihnachtsfeierBit.Framework;

public static class FrameworkServiceCollectionExtensions
{
    public static IServiceCollection AddWeihnachtsfeierFramework(
        this IServiceCollection services,
        Action<HttpClient> configureApiClient)
    {
        services.AddScoped<ClientDiagnosticLogger>();
        services.AddHttpClient<ProjectApiClient>(configureApiClient);
        return services;
    }
}