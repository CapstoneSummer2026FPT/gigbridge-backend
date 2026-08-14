using Application.Features.JobInvitations.Common.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.JobInvitations.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddJobInvitationServices(this IServiceCollection services)
    {
        services.AddSingleton<IJobInvitationEmailRenderer, JobInvitationEmailRenderer>();
        return services;
    }
}
