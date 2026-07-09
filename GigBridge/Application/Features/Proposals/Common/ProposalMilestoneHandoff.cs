using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common;

internal static class ProposalMilestoneHandoff
{
    public static async Task SeedConversationDraftAsync(
        IApplicationDbContext context,
        Guid conversationId,
        Proposal proposal,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasDraft = await context.Set<NegotiationMilestoneDraft>()
            .AnyAsync(item => item.ConversationsId == conversationId, cancellationToken);

        if (hasDraft) return;

        foreach (var plan in proposal.ProposalMilestonePlans.OrderBy(item => item.OrderIndex))
        {
            context.Set<NegotiationMilestoneDraft>().Add(new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = Guid.NewGuid(),
                ConversationsId = conversationId,
                SourceProposalMilestonePlanId = plan.ProposalMilestonePlansId,
                Title = plan.Title,
                Description = plan.Description,
                Amount = plan.Amount,
                EstimatedDuration = plan.EstimatedDuration,
                Deliverables = plan.Deliverables ?? string.Empty,
                AcceptanceCriteria = plan.AcceptanceCriteria ?? string.Empty,
                OrderIndex = plan.OrderIndex,
                CreatedAt = now
            });
        }
    }
}
