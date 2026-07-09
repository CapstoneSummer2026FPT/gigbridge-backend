using Application.Common.Exceptions;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalSubmissionGuard
{
    public static void EnsureCanSubmit(Proposal proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.CoverLetter) || proposal.CoverLetter.Trim().Length < 50)
        {
            throw new BadRequestException("Cover letter must be at least 50 characters before submission.");
        }

        if (!proposal.ProposedBudget.HasValue || proposal.ProposedBudget.Value <= 0)
        {
            throw new BadRequestException("Proposed budget must be greater than zero before submission.");
        }

        if (string.IsNullOrWhiteSpace(proposal.AnalysisSummary) || proposal.AnalysisSummary.Trim().Length < 50)
        {
            throw new BadRequestException("Requirement analysis must be at least 50 characters before submission.");
        }

        if (string.IsNullOrWhiteSpace(proposal.SolutionApproach) || proposal.SolutionApproach.Trim().Length < 50)
        {
            throw new BadRequestException("Solution approach must be at least 50 characters before submission.");
        }

        if (proposal.ProposalWorkBreakdownItems.Count == 0 ||
            proposal.ProposalWorkBreakdownItems.Any(item => string.IsNullOrWhiteSpace(item.Title)))
        {
            throw new BadRequestException("At least one titled work breakdown item is required before submission.");
        }

        if (proposal.ProposalMilestonePlans.Count == 0 ||
            proposal.ProposalMilestonePlans.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Deliverables) ||
                string.IsNullOrWhiteSpace(item.AcceptanceCriteria) ||
                !ProposalTotalsCalculator.IsValidDuration(item.EstimatedDuration) ||
                item.Amount <= 0))
        {
            throw new BadRequestException("Each milestone requires a title, positive amount, positive whole-number duration, deliverables, and acceptance criteria before submission.");
        }
    }
}
