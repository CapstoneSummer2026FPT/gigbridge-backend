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
            var draft = new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = Guid.NewGuid(),
                ConversationsId = conversationId,
                SourceProposalMilestonePlanId = plan.ProposalMilestonePlansId,
                Title = plan.Title,
                Description = plan.Description,
                Amount = plan.Amount,
                EstimatedDuration = plan.EstimatedDuration,
                DueDate = plan.DueDate,
                Deliverables = plan.Deliverables ?? string.Empty,
                AcceptanceCriteria = plan.AcceptanceCriteria ?? string.Empty,
                OrderIndex = plan.OrderIndex,
                CreatedAt = now
            };
            draft.WorkItems = proposal.ProposalWorkBreakdownItems
                .Where(item => item.ProposalMilestonePlansId == plan.ProposalMilestonePlansId)
                .OrderBy(item => item.OrderIndex)
                .Select((item, index) => new NegotiationMilestoneDraftWorkItem
                {
                    NegotiationMilestoneDraftWorkItemId = Guid.NewGuid(),
                    NegotiationMilestoneDraftId = draft.NegotiationMilestoneDraftId,
                    Title = item.Title,
                    Description = item.Description,
                    Deliverables = item.Deliverables,
                    EstimatedDuration = item.EstimatedDuration,
                    OrderIndex = index
                }).ToList();
            context.Set<NegotiationMilestoneDraft>().Add(draft);
        }
    }
}
