using Application.Common.Options;
using Application.Common.InternalServices.Receipts.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Receipts;

internal static class DependencyInjection
{
    internal static IServiceCollection AddReceiptServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddHostedService<ProjectReceiptWorker>();
        }

        return services;
    }
}
