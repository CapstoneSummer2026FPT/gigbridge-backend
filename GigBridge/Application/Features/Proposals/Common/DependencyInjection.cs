using Application.Features.Proposals.Common.Email;
using Application.Features.Proposals.Common.Interfaces;
using Application.Features.Proposals.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Proposals.Common;

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
