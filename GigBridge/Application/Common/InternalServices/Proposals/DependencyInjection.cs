using Application.Common.InternalServices.Proposals.Email;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Proposals;
internal static class DependencyInjection
{
    internal static IServiceCollection AddProposalServices(this IServiceCollection services)
    {
        services.AddScoped<IProposalQuestionTimerService, ProposalQuestionTimerService>();
        services.AddScoped<IProposalInterviewReviewService, ProposalInterviewReviewService>();
        services.AddSingleton<IProposalNegotiationEmailRenderer, ProposalNegotiationEmailRenderer>();
        return services;
    }
}
