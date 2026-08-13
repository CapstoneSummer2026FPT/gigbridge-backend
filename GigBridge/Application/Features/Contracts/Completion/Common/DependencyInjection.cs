using Application.Common.Options;
using Application.Features.Contracts.Completion.Common.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Features.Contracts.Completion.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddContractCompletionBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddSingleton<ContractAutoCompletionWorker>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<ContractAutoCompletionWorker>());
        }

        return services;
    }
}
