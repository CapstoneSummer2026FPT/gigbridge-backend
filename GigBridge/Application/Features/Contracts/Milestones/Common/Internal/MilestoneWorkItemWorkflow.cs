using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;

namespace Application.Features.Contracts.Milestones.Common.Internal;

/// <summary>
/// What happened to the milestone as a side effect of a work item transition, so the caller can
/// fire the right realtime event, notification and email without re-deriving it.
/// </summary>
internal readonly record struct MilestoneTransition(
    bool MilestoneStarted,
    bool MilestoneSubmitted,
    bool MilestoneApproved,
    bool MilestoneReopened,
    Milestone? NextMilestone);

/// <summary>
/// Reconciles a milestone against its work items. Pure state transitions on already-loaded entities —
/// no DI, no I/O — mirroring <see cref="MilestoneWorkflowGuard"/>.
///
/// Callers MUST have loaded the milestone and its complete work item set *after* taking the milestone
/// row lock. Reconciling against a set read before the lock is exactly how two concurrent submissions
/// both miss that the milestone became fully submitted.
/// </summary>
internal static class MilestoneWorkItemWorkflow
{
    /// <summary>
    /// Pulls a Pending milestone into InProgress on the freelancer's first submission, mirroring the
    /// side effect that used to live inline in UpdateContractWorkItemCommandHandler so both the legacy
    /// checkbox path and the work item delivery path start milestones identically.
    /// </summary>
    public static bool TryStart(
        Milestone milestone,
        IReadOnlyList<Milestone> orderedMilestones,
        bool hasApprovedEarlyStartRequest,
        DateTime now)
    {
        if (milestone.Status != (int)MilestoneStatus.Pending)
        {
            return false;
        }

        if (!MilestoneWorkflowGuard.IsEligibleToStart(milestone, orderedMilestones, hasApprovedEarlyStartRequest))
        {
            throw new BadRequestException(
                "Finish the previous milestones first, or ask the client to approve an early start for this one.");
        }

        milestone.Status = (int)MilestoneStatus.InProgress;
        milestone.StartedAt ??= now;
        milestone.UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// After a batch of submissions: the milestone reaches Submitted only once every work item is
    /// either awaiting review or already approved. Partial submission leaves it InProgress.
    /// </summary>
    public static MilestoneTransition ApplyAfterSubmit(
        Milestone milestone,
        IReadOnlyList<ContractWorkItem> items,
        DateTime now)
    {
        if (items.Count == 0)
        {
            return default;
        }

        var everyItemDelivered = items.All(item =>
            ContractWorkItemStatusExtensions.IsAwaitingReview(item.Status) ||
            ContractWorkItemStatusExtensions.IsDelivered(item.Status));

        if (!everyItemDelivered || milestone.Status == (int)MilestoneStatus.Submitted)
        {
            return default;
        }

        milestone.Status = (int)MilestoneStatus.Submitted;
        milestone.SubmittedAt = now;
        milestone.UpdatedAt = now;
        return new MilestoneTransition(false, true, false, false, null);
    }

    /// <summary>
    /// After a batch of client decisions. Two outcomes:
    /// every item approved closes the milestone and opens the next one; any item sent back for revision
    /// reopens a Submitted milestone so the freelancer can work again.
    /// </summary>
    public static MilestoneTransition ApplyAfterReview(
        Milestone milestone,
        IReadOnlyList<ContractWorkItem> items,
        IReadOnlyList<Milestone> orderedMilestones,
        DateTime now)
    {
        if (items.Count == 0)
        {
            return default;
        }

        if (items.All(item => ContractWorkItemStatusExtensions.IsDelivered(item.Status)))
        {
            if (milestone.Status == (int)MilestoneStatus.Approved)
            {
                return default;
            }

            milestone.Status = (int)MilestoneStatus.Approved;

            // ContractAutoCompletionWorker filters on !ApprovedAt.HasValue. Skip this stamp and the
            // contract silently never auto-completes.
            milestone.ApprovedAt ??= now;
            milestone.UpdatedAt = now;

            var before = orderedMilestones
                .Where(candidate => candidate.Status == (int)MilestoneStatus.InProgress)
                .Select(candidate => candidate.MilestonesId)
                .ToHashSet();

            MilestoneWorkflowGuard.AdvanceNextMilestone(orderedMilestones, now);

            var next = orderedMilestones.FirstOrDefault(candidate =>
                candidate.Status == (int)MilestoneStatus.InProgress &&
                !before.Contains(candidate.MilestonesId));

            return new MilestoneTransition(false, false, true, false, next);
        }

        var anyNeedsRevision = items.Any(item => item.Status == (int)ContractWorkItemStatus.RevisionRequired);
        if (anyNeedsRevision && milestone.Status == (int)MilestoneStatus.Submitted)
        {
            milestone.Status = (int)MilestoneStatus.InProgress;
            milestone.SubmittedAt = null;
            milestone.UpdatedAt = now;
            return new MilestoneTransition(false, false, false, true, null);
        }

        return default;
    }

    /// <summary>
    /// Forces the work items of a milestone into a state consistent with an outcome an admin imposed
    /// while resolving a dispute.
    ///
    /// Without this, a dispute that force-approves a milestone leaves its work items on Todo/Submitted:
    /// the delivery space shows a finished milestone full of pending work, and if the contract resumes,
    /// approving one straggler runs <see cref="ApplyAfterReview"/> a second time — duplicate completion
    /// email, and the following milestone reopened.
    ///
    /// An item that was never submitted stays Todo. Labelling it RevisionRequired would assert the
    /// freelancer delivered something the client rejected, which never happened.
    /// </summary>
    public static void SyncWorkItemsToResolvedMilestone(
        Milestone milestone,
        IReadOnlyList<ContractWorkItem> items,
        DateTime now)
    {
        foreach (var item in items)
        {
            switch ((MilestoneStatus)milestone.Status)
            {
                case MilestoneStatus.Approved:
                case MilestoneStatus.Completed:
                    if (!ContractWorkItemStatusExtensions.IsDelivered(item.Status))
                    {
                        item.Status = (int)ContractWorkItemStatus.Approved;
                        item.CompletedAt ??= now;
                        item.UpdatedAt = now;
                    }

                    break;

                case MilestoneStatus.InProgress:
                    if (ContractWorkItemStatusExtensions.IsAwaitingReview(item.Status))
                    {
                        item.Status = (int)ContractWorkItemStatus.RevisionRequired;
                        item.CompletedAt = null;
                        item.UpdatedAt = now;
                    }

                    break;
            }
        }
    }
}
