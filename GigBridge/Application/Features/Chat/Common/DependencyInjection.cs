using Application.Common.Options;
using Application.Features.Chat.Common.FinalOffers.Shared.Email;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Chat.Common.Schedules.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Chat.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddChatServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ScheduleWorkflowService>();
        services.AddSingleton<IScheduleEmailRenderer, ScheduleEmailRenderer>();
        services.AddSingleton<IJobAcceptanceEmailRenderer, JobAcceptanceEmailRenderer>();

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddHostedService<GoogleMeetProvisioningWorker>();
        }

        return services;
    }
}
