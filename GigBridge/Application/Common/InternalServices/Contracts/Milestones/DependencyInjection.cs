using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Milestones.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Contracts.Milestones;
internal static class DependencyInjection
{
    internal static IServiceCollection AddMilestoneSubmissionServices(this IServiceCollection services)
    {
        services.AddSingleton<IMilestoneSubmissionEmailRenderer, MilestoneSubmissionEmailRenderer>();
        services.AddSingleton<IWorkItemDeliveryEmailRenderer, WorkItemDeliveryEmailRenderer>();
        return services;
    }
}
