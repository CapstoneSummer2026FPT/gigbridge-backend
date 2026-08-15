using Application.Common.InternalServices.JobInvitations.Email;
using Application.Common.InternalServices.JobInvitations.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.JobInvitations;
internal static class DependencyInjection
{
    internal static IServiceCollection AddJobInvitationServices(this IServiceCollection services)
    {
        services.AddSingleton<IJobInvitationEmailRenderer, JobInvitationEmailRenderer>();
        return services;
    }
}
