using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Domain.Entities;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class JobPostSetupPublishGuard
{
    public const int DraftStatus = 0;
    public const int OpenStatus = 1;
    private const string DefaultDraftTitle = "Untitled Job Post";

    public static void EnsureProjectRequestCanPublish(JobPost jobPost, DateOnly today)
    {
        if (jobPost.Status != DraftStatus && jobPost.Status != OpenStatus)
        {
            throw new BadRequestException("Project request must be in Draft or Open status to publish.");
        }

        if (string.IsNullOrWhiteSpace(jobPost.Title) ||
            string.Equals(jobPost.Title.Trim(), DefaultDraftTitle, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Project request title is required before publishing.");
        }

        if (string.IsNullOrWhiteSpace(jobPost.Description))
        {
            throw new BadRequestException("Project requirement details are required before publishing.");
        }

        if (!jobPost.MajorCategoryId.HasValue)
        {
            throw new BadRequestException("Project request category is required before publishing.");
        }


        var milestones = jobPost.JobPostMilestonePlans.OrderBy(item => item.OrderIndex).ToList();
        if (milestones.Select(item => item.OrderIndex).Distinct().Count() != milestones.Count)
        {
            throw new BadRequestException("Milestone order indexes must be unique.");
        }

        var applicationDeadline = jobPost.EndDate.HasValue
            ? DateOnly.FromDateTime(jobPost.EndDate.Value)
            : (DateOnly?)null;
        DateOnly? previousDueDate = null;

        foreach (var milestone in milestones)
        {
            if (string.IsNullOrWhiteSpace(milestone.Title) ||
                milestone.Amount <= 0 ||
                !ProposalTotalsCalculator.IsValidProjectDuration(milestone.EstimatedDuration) ||
                !milestone.DueDate.HasValue ||
                string.IsNullOrWhiteSpace(milestone.Deliverables) ||
                string.IsNullOrWhiteSpace(milestone.AcceptanceCriteria))
            {
                throw new BadRequestException("Each client milestone requires a title, positive amount, week/month/year duration, deadline, deliverables, and acceptance criteria.");
            }

            var dueDate = milestone.DueDate.Value;
            if (dueDate < today)
            {
                throw new BadRequestException("Milestone deadlines cannot be in the past.");
            }

            if (applicationDeadline.HasValue && dueDate <= applicationDeadline.Value)
            {
                throw new BadRequestException("Milestone deadlines must be after the project request application deadline.");
            }

            if (previousDueDate.HasValue && dueDate <= previousDueDate.Value)
            {
                throw new BadRequestException("Milestone deadlines must increase according to milestone order.");
            }
            previousDueDate = dueDate;

            if (milestone.WorkItems.Select(item => item.OrderIndex).Distinct().Count() != milestone.WorkItems.Count)
            {
                throw new BadRequestException("Work item order indexes must be unique within each milestone.");
            }

            if (milestone.WorkItems.Any(item =>
                    string.IsNullOrWhiteSpace(item.Title) ||
                    string.IsNullOrWhiteSpace(item.Description)))
            {
                throw new BadRequestException("Each client work item requires a title and description.");
            }
        }
    }

    public static async Task EnsureCanPublishAsync(
        IApplicationDbContext context,
        JobPost jobPost,
        Contract? contract,
        CancellationToken cancellationToken)
    {
        if (jobPost.Status != DraftStatus && jobPost.Status != OpenStatus)
        {
            throw new BadRequestException("Job post must be in Draft or Open status to complete setup.");
        }

        if (contract is null)
        {
            throw new BadRequestException("Draft contract must exist before publishing the job post.");
        }

        var isEsignFullySigned = await context.Set<EsignDocument>()
            .AnyAsync(
                document =>
                    document.JobPostsId == jobPost.JobPostsId &&
                    document.Status == (int)ESignDocumentStatus.FullySigned,
                cancellationToken);

        if (!isEsignFullySigned)
        {
            throw new BadRequestException("Job post e-sign document is not fully signed.");
        }

        if (contract.Milestones.Count == 0)
        {
            throw new BadRequestException("At least one milestone is required.");
        }

        foreach (var milestone in contract.Milestones)
        {
            if (string.IsNullOrWhiteSpace(milestone.Title))
            {
                throw new BadRequestException("Milestone title cannot be empty.");
            }

            if (milestone.Amount <= 0)
            {
                throw new BadRequestException("Milestone amount must be positive.");
            }
        }

        ContractDetailsValidator.ValidateMilestoneTotalDoesNotExceedBudget(contract, contract.Milestones.ToList());
    }
}
