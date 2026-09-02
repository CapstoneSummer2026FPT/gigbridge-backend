using Application.Common.InternalServices.Scheduling;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums.Disputes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Client.RemainingJobPostPlan.Queries;

public sealed class GetDisputeRemainingJobPostPlanQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDisputeRemainingJobPostPlanQuery, DisputeRemainingJobPostPlanResponse>
{
    private const decimal DefaultMilestoneWeeks = 2m;

    public async Task<DisputeRemainingJobPostPlanResponse> Handle(
        GetDisputeRemainingJobPostPlanQuery request, CancellationToken cancellationToken)
    {
        var dispute = await context.Set<Dispute>()
            .Include(item => item.Contracts).ThenInclude(contract => contract.ClientProfiles)
            .Include(item => item.Contracts).ThenInclude(contract => contract.JobPosts).ThenInclude(jobPost => jobPost.JobPostSkills)
            .Include(item => item.Contracts).ThenInclude(contract => contract.Milestones).ThenInclude(milestone => milestone.WorkItems)
            .FirstOrDefaultAsync(item => item.DisputesId == request.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var contract = dispute.Contracts;
        if (contract.ClientProfiles.UserId != request.UserId)
            throw new ForbiddenAccessException("Only the client on this contract may recreate its job post.");

        if (dispute.Status != (int)DisputeStatus.Resolved)
            throw new ConflictException("The dispute has not been resolved yet.");

        var remaining = DisputeJobPostRecreationSupport.GetRemainingMilestones(contract.Milestones);
        if (!DisputeJobPostRecreationSupport.IsEligible(contract.Status, contract.Milestones) || remaining.Count == 0)
            throw new ConflictException(
                "This dispute is not eligible for job post recreation. The contract must have been " +
                "cancelled with unfinished milestones remaining.");

        var jobPost = contract.JobPosts;
        var resolvedAt = dispute.ResolvedAt
            ?? throw new ConflictException("The dispute is missing a resolution timestamp.");

        var originalJobWeeks = DisputeJobPostRecreationSupport.ParseWeeksOrNull(jobPost.EstimatedDuration) ?? 0m;
        var fallbackWeeksPerMilestone = originalJobWeeks > 0 ? originalJobWeeks / remaining.Count : DefaultMilestoneWeeks;

        var milestoneWeeks = remaining
            .Select(milestone => DisputeJobPostRecreationSupport.ParseWeeksOrNull(milestone.EstimatedDuration) ?? fallbackWeeksPerMilestone)
            .ToList();
        var remainingWeeksTotal = milestoneWeeks.Sum();

        var newEndDate = resolvedAt.AddDays((double)(remainingWeeksTotal * 7));

        var milestonePlans = new List<DisputeRemainingMilestonePlanResponse>();
        var previousDueDate = newEndDate;
        for (var index = 0; index < remaining.Count; index++)
        {
            var milestone = remaining[index];
            var days = Math.Ceiling(milestoneWeeks[index] * 7);
            var dueDate = previousDueDate.AddDays((double)days);

            // Work item deadlines are rebuilt inside the milestone's new window, anchored on the day
            // before it starts, exactly as they were on the original contract.
            var orderedWorkItems = (milestone.WorkItems ?? [])
                .OrderBy(workItem => workItem.OrderIndex)
                .ToList();
            var workItemDueDates = WorkBreakdownScheduleCalculator.CalculateWorkItemDueDates(
                DateOnly.FromDateTime(previousDueDate),
                orderedWorkItems.Select(workItem => workItem.EstimatedDuration).ToList());

            previousDueDate = dueDate;

            milestonePlans.Add(new DisputeRemainingMilestonePlanResponse(
                milestone.Title,
                milestone.Description,
                milestone.Amount,
                milestone.EstimatedDuration,
                DateOnly.FromDateTime(dueDate),
                milestone.Deliverables,
                milestone.AcceptanceCriteria,
                index,
                orderedWorkItems.Select((workItem, workIndex) => new DisputeRemainingWorkItemPlanResponse(
                    workItem.Title,
                    workItem.Description,
                    workItem.EstimatedDuration,
                    workIndex < workItemDueDates.Count ? workItemDueDates[workIndex] : null,
                    workIndex)).ToList()));
        }

        return new DisputeRemainingJobPostPlanResponse(
            contract.ContractsId,
            jobPost.JobPostsId,
            jobPost.Title,
            jobPost.Description,
            jobPost.MajorCategoryId,
            jobPost.Currency,
            jobPost.Visibility,
            jobPost.CustomSkillNames,
            jobPost.JobPostSkills.Select(skill => skill.SkillsId).ToArray(),
            remaining.Sum(milestone => milestone.Amount),
            DisputeJobPostRecreationSupport.FormatWeeks(remainingWeeksTotal),
            newEndDate,
            resolvedAt,
            milestonePlans);
    }
}
