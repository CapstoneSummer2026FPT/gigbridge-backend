using Application.Common.Exceptions;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalSubmissionGuard
{
    public static void EnsureCanSubmit(Proposal proposal, DateOnly today)
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
            proposal.ProposalWorkBreakdownItems.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                !item.ProposalMilestonePlansId.HasValue))
        {
            throw new BadRequestException("Every work breakdown item must have a title, description, and belong to a milestone before submission.");
        }

        if (proposal.ProposalMilestonePlans.Any(milestone =>
                !proposal.ProposalWorkBreakdownItems.Any(item =>
                    item.ProposalMilestonePlansId == milestone.ProposalMilestonePlansId)))
        {
            throw new BadRequestException("Each milestone requires at least one work breakdown item before submission.");
        }

        if (proposal.ProposalMilestonePlans.Count == 0 ||
            proposal.ProposalMilestonePlans.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Deliverables) ||
                string.IsNullOrWhiteSpace(item.AcceptanceCriteria) ||
                !ProposalTotalsCalculator.IsValidProjectDuration(item.EstimatedDuration) ||
                !item.DueDate.HasValue ||
                item.Amount <= 0))
        {
            throw new BadRequestException("Each milestone requires a title, positive amount, week/month/year duration, deadline, deliverables, and acceptance criteria before submission.");
        }

        var applicationDeadline = proposal.JobPosts.EndDate.HasValue
            ? DateOnly.FromDateTime(proposal.JobPosts.EndDate.Value)
            : (DateOnly?)null;
        DateOnly? previousDueDate = null;
        foreach (var milestone in proposal.ProposalMilestonePlans.OrderBy(item => item.OrderIndex))
        {
            var dueDate = milestone.DueDate.GetValueOrDefault();
            if (dueDate < today)
            {
                throw new BadRequestException("Proposal milestone deadlines cannot be in the past.");
            }

            if (applicationDeadline.HasValue && dueDate <= applicationDeadline.Value)
            {
                throw new BadRequestException("Proposal milestone deadlines must be after the project request application deadline.");
            }

            if (previousDueDate.HasValue && dueDate <= previousDueDate.Value)
            {
                throw new BadRequestException("Proposal milestone deadlines must increase according to milestone order.");
            }
            previousDueDate = dueDate;
        }
    }
}
