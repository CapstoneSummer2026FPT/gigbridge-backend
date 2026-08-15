using Application.Features.Contracts.Milestones.Freelancer.Submit.Common.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddMilestoneSubmissionServices(this IServiceCollection services)
    {
        services.AddSingleton<IMilestoneSubmissionEmailRenderer, MilestoneSubmissionEmailRenderer>();
        return services;
    }
}
