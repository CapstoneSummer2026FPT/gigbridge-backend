using Application.Common.InternalServices.MarketplaceAnalytics.Interfaces;
using Application.Common.InternalServices.MarketplaceAnalytics.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.MarketplaceAnalytics;
internal static class DependencyInjection
{
    internal static IServiceCollection AddMarketplaceAnalyticsServices(this IServiceCollection services)
    {
        services.AddScoped<IMarketplaceAnalyticsRecorder, MarketplaceAnalyticsRecorder>();
        return services;
    }
}
