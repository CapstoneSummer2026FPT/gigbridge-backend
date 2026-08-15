using Application.Common.Options;
using Application.Common.InternalServices.Chat.BackgroundJobs;
using Application.Common.InternalServices.Chat.Email;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Chat;
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
