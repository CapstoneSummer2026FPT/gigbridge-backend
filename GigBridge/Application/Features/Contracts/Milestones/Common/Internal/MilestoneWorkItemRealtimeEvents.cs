using Application.Common.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Milestones.Common.Internal;

/// <summary>
/// Realtime announcements for the work item delivery flow.
///
/// Delivered per user via <see cref="IChatRealtimeNotifier.SendUsersEventAsync"/> rather than to the
/// conversation group, because the workspace and delivery space never call ChatHub.JoinConversation —
/// a group event would simply never arrive. Recipients come from
/// <see cref="MilestoneWorkflowGuard.GetParticipantUserIdsAsync"/>, which resolves the client and
/// freelancer directly and therefore works even on contracts that have no workroom conversation.
///
/// Every publish is wrapped in one swallowing try/catch and is called AFTER the transaction commits
/// with <c>CancellationToken.None</c>: a dropped SignalR frame must never roll back a submission that
/// already happened, and a client disconnecting mid-request must not skip the other party's update.
/// </summary>
internal static class MilestoneWorkItemRealtimeEvents
{
    public const string WorkItemSubmitted = "WorkItemSubmitted";
    public const string WorkItemReviewed = "WorkItemReviewed";
    public const string MilestoneAutoCompleted = "MilestoneAutoCompleted";

    public static async Task PublishWorkItemSubmittedAsync(
        IApplicationDbContext context,
        IChatRealtimeNotifier? notifier,
        ILogger? logger,
        Contract contract,
        Milestone milestone,
        IReadOnlyList<Guid> workItemIds,
        Guid submissionBatchId,
        bool milestoneSubmitted,
        CancellationToken cancellationToken)
    {
        if (notifier is null)
        {
            return;
        }

        try
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(
                context, contract, cancellationToken);
            if (participantIds.Count == 0)
            {
                return;
            }

            await notifier.SendUsersEventAsync(
                participantIds,
                WorkItemSubmitted,
                new
                {
                    contractId = contract.ContractsId,
                    milestoneId = milestone.MilestonesId,
                    workItemIds,
                    submissionBatchId,
                    milestoneStatus = milestone.Status,
                    milestoneSubmitted
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning(
                exception,
                "Failed to publish {Event} for milestone {MilestoneId}.",
                WorkItemSubmitted,
                milestone.MilestonesId);
        }
    }

    /// <param name="eventId">
    /// Stable per completion, so the client can open the "milestone complete" modal exactly once even
    /// though it learns about the same completion twice — from its own HTTP response and from SignalR.
    /// </param>
    public static async Task PublishWorkItemReviewedAsync(
        IApplicationDbContext context,
        IChatRealtimeNotifier? notifier,
        ILogger? logger,
        Contract contract,
        Milestone milestone,
        IReadOnlyList<Guid> workItemIds,
        bool approved,
        MilestoneTransition transition,
        string eventId,
        CancellationToken cancellationToken)
    {
        if (notifier is null)
        {
            return;
        }

        try
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(
                context, contract, cancellationToken);
            if (participantIds.Count == 0)
            {
                return;
            }

            await notifier.SendUsersEventAsync(
                participantIds,
                WorkItemReviewed,
                new
                {
                    contractId = contract.ContractsId,
                    milestoneId = milestone.MilestonesId,
                    workItemIds,
                    approved,
                    milestoneStatus = milestone.Status,
                    milestoneReopened = transition.MilestoneReopened
                },
                cancellationToken);

            if (!transition.MilestoneApproved)
            {
                return;
            }

            await notifier.SendUsersEventAsync(
                participantIds,
                MilestoneAutoCompleted,
                new
                {
                    eventId,
                    contractId = contract.ContractsId,
                    milestoneId = milestone.MilestonesId,
                    milestoneTitle = milestone.Title,
                    status = milestone.Status,
                    approvedAt = milestone.ApprovedAt,
                    nextMilestoneId = transition.NextMilestone?.MilestonesId,
                    nextMilestoneTitle = transition.NextMilestone?.Title
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning(
                exception,
                "Failed to publish {Event} for milestone {MilestoneId}.",
                WorkItemReviewed,
                milestone.MilestonesId);
        }
    }

    /// <summary>
    /// Deterministic id for one milestone completion. Derived from the milestone and its approval
    /// timestamp so the HTTP response and the SignalR frame carry the same value.
    /// </summary>
    public static string BuildCompletionEventId(Milestone milestone) =>
        $"{MilestoneAutoCompleted}:{milestone.MilestonesId:D}:{milestone.ApprovedAt:O}";
}
