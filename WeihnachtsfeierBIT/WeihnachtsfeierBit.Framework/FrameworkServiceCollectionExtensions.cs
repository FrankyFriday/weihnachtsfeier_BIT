using Microsoft.Extensions.DependencyInjection;

namespace WeihnachtsfeierBit.Framework;

public static class FrameworkServiceCollectionExtensions
{
    public static IServiceCollection AddWeihnachtsfeierFramework(
        this IServiceCollection services,
        Action<HttpClient> configureApiClient)
    {
        services.AddHttpClient<ProjectApiClient>(configureApiClient);
        return services;
    }
}