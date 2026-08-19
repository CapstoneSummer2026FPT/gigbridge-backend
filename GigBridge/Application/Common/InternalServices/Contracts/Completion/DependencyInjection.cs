using Application.Common.Options;
using Application.Common.InternalServices.Contracts.Completion.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Common.InternalServices.Contracts.Completion;
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
