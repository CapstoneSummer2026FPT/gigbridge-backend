using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Contracts.Milestones.WorkItems.Common;
using Application.Features.Contracts.Milestones.WorkItems.Freelancer.Submit.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Delivery;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Milestones.WorkItems.Client.Review.Commands;

/// <summary>
/// Records the client's verdict on a batch of work items, in any order and over any subset.
///
/// Approve and request-revision run through one handler so the two verdicts can never disagree about
/// what the milestone status should become — the reconciliation that closes a milestone and the one
/// that reopens it are literally the same call.
/// </summary>
public sealed class ReviewContractWorkItemsCommandHandler :
    IRequestHandler<ReviewContractWorkItemsCommand, ReviewWorkItemsResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<ReviewContractWorkItemsCommandHandler>? _logger;

    public ReviewContractWorkItemsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserAuditLogService userAuditLog,
        IChatRealtimeNotifier? realtimeNotifier = null,
        INotificationService? notificationService = null,
        ILogger<ReviewContractWorkItemsCommandHandler>? logger = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userAuditLog = userAuditLog;
        _realtimeNotifier = realtimeNotifier;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ReviewWorkItemsResponse> Handle(
        ReviewContractWorkItemsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureClientAsync(
            _context, contract, command.UserId, cancellationToken);
        MilestoneDeliveryModeGuard.EnsureWorkItemDelivery(contract);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context, command.ContractId, command.MilestoneId, cancellationToken);

        ValidateRequestShape(command);

        var now = _dateTimeService.UtcNow;
        MilestoneTransition transition;
        List<ContractWorkItem> reviewed;
        Message? systemMessage;

        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            // Same milestone lock the submit path takes: a bulk approve arriving while the
            // freelancer submits the last item must not reconcile against a stale item set.
            await transaction.AcquireTransactionLockAsync(
                MilestoneDeliveryLock.ForMilestone(milestone.MilestonesId),
                cancellationToken,
                lockPurpose: "work-item-review");

            var items = await _context.Set<ContractWorkItem>()
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync(cancellationToken);

            reviewed = ResolveTargets(command, items);

            var pendingReview = reviewed
                .Where(item => ContractWorkItemStatusExtensions.IsAwaitingReview(item.Status))
                .ToList();

            // Nothing left to decide — a duplicate click, or another tab got there first.
            // Return the committed state instead of re-firing notifications and email.
            if (pendingReview.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                var unchanged = await BuildResponseAsync(contract, milestone.MilestonesId, cancellationToken);
                return new ReviewWorkItemsResponse(unchanged, false, null, null);
            }

            await ApplyVerdictAsync(command, pendingReview, now, cancellationToken);

            var orderedMilestones = await MilestoneWorkflowGuard.OrderMilestones(
                    _context.Set<Milestone>().Where(item => item.ContractsId == contract.ContractsId))
                .ToListAsync(cancellationToken);

            transition = MilestoneWorkItemWorkflow.ApplyAfterReview(
                milestone, items, orderedMilestones, now);

            if (transition.NextMilestone is not null)
            {
                await MilestoneEarlyStartRequestWorkflow.CancelPendingForMilestoneAsync(
                    _context,
                    transition.NextMilestone.MilestonesId,
                    now,
                    cancellationToken);
            }

            contract.UpdatedAt = now;

            systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
                _context,
                contract.ContractsId,
                BuildSystemMessage(command, milestone, pendingReview, transition),
                now,
                cancellationToken);

            _userAuditLog.Add(
                command.UserId,
                UserRole.Client,
                command.Approve ? AuditUserActionType.MilestoneApproved : AuditUserActionType.MilestoneSubmitted,
                contract.ContractsId,
                command.Approve
                    ? $"Approved {pendingReview.Count} work item(s) on milestone: {milestone.Title}."
                    : $"Requested revision on {pendingReview.Count} work item(s) of milestone: {milestone.Title}.",
                milestoneId: milestone.MilestonesId);

            await EnqueueEmailAsync(contract, milestone, pendingReview, command, transition, now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            reviewed = pendingReview;
        }

        await NotifyFreelancerAsync(contract, milestone, reviewed, command, transition, CancellationToken.None);

        var eventId = MilestoneWorkItemRealtimeEvents.BuildCompletionEventId(milestone);
        await MilestoneWorkItemRealtimeEvents.PublishWorkItemReviewedAsync(
            _context,
            _realtimeNotifier,
            _logger,
            contract,
            milestone,
            reviewed.Select(item => item.ContractWorkItemId).ToList(),
            command.Approve,
            transition,
            eventId,
            CancellationToken.None);

        if (systemMessage is not null && _realtimeNotifier is not null)
        {
            await PublishSystemMessageAsync(systemMessage);
        }

        var response = await BuildResponseAsync(contract, milestone.MilestonesId, CancellationToken.None);

        return new ReviewWorkItemsResponse(
            response,
            transition.MilestoneApproved,
            transition.NextMilestone?.MilestonesId,
            transition.NextMilestone?.Title);
    }

    private static void ValidateRequestShape(ReviewContractWorkItemsCommand command)
    {
        if (command.WorkItemIds.Count == 0)
        {
            throw new BadRequestException("Select at least one work item to review.");
        }

        if (command.WorkItemIds.Distinct().Count() != command.WorkItemIds.Count)
        {
            throw new BadRequestException("The same work item cannot appear twice in one review.");
        }

        if (!command.Approve && string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new BadRequestException("A reason is required when requesting a revision.");
        }

        if (command.Reason is { Length: > 2000 })
        {
            throw new BadRequestException("The revision reason exceeds 2000 characters.");
        }
    }

    private static List<ContractWorkItem> ResolveTargets(
        ReviewContractWorkItemsCommand command,
        List<ContractWorkItem> items)
    {
        var byId = items.ToDictionary(item => item.ContractWorkItemId);
        var targeted = new List<ContractWorkItem>(command.WorkItemIds.Count);

        foreach (var id in command.WorkItemIds)
        {
            if (!byId.TryGetValue(id, out var item))
            {
                throw new NotFoundException("A selected work item does not belong to this milestone.");
            }

            targeted.Add(item);
        }

        return targeted;
    }

    /// <summary>
    /// Writes the verdict onto both the aggregate work item status and the latest attempt, so the
    /// history keeps the reason and the workflow keeps a cheap status to query.
    /// </summary>
    private async Task ApplyVerdictAsync(
        ReviewContractWorkItemsCommand command,
        List<ContractWorkItem> pendingReview,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ids = pendingReview.Select(item => item.ContractWorkItemId).ToList();

        var attempts = await _context.Set<ContractWorkItemSubmission>()
            .Where(submission => ids.Contains(submission.ContractWorkItemId))
            .ToListAsync(cancellationToken);

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

        foreach (var item in pendingReview)
        {
            item.Status = command.Approve
                ? (int)ContractWorkItemStatus.Approved
                : (int)ContractWorkItemStatus.RevisionRequired;
            item.CompletedAt = command.Approve ? now : null;
            item.UpdatedAt = now;

            // Only the newest attempt is still open for a decision; earlier ones are settled history.
            var latest = attempts
                .Where(submission => submission.ContractWorkItemId == item.ContractWorkItemId)
                .OrderByDescending(submission => submission.RevisionNumber)
                .FirstOrDefault();

            if (latest is null)
            {
                continue;
            }

            latest.ReviewStatus = command.Approve
                ? (int)ContractWorkItemSubmissionReviewStatus.Approved
                : (int)ContractWorkItemSubmissionReviewStatus.RevisionRequired;
            latest.ReviewedAt = now;
            latest.ReviewedByUserId = command.UserId;
            latest.ReviewReason = reason;
        }
    }

    private static string BuildSystemMessage(
        ReviewContractWorkItemsCommand command,
        Milestone milestone,
        List<ContractWorkItem> reviewed,
        MilestoneTransition transition)
    {
        if (transition.MilestoneApproved)
        {
            return $"Milestone completed: {milestone.Title}.";
        }

        return command.Approve
            ? $"{reviewed.Count} work item(s) approved on milestone: {milestone.Title}."
            : $"Revision requested on {reviewed.Count} work item(s) of milestone: {milestone.Title}.";
    }

    /// <summary>
    /// Email policy: a submission, a revision request and a milestone completion each earn one mail.
    /// A partial approval does not — the freelancer would otherwise get a message every time the
    /// client ticks one box, which is exactly the noise that makes people mute notifications.
    /// </summary>
    private async Task EnqueueEmailAsync(
        Contract contract,
        Milestone milestone,
        List<ContractWorkItem> reviewed,
        ReviewContractWorkItemsCommand command,
        MilestoneTransition transition,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (command.Approve && !transition.MilestoneApproved)
        {
            return;
        }

        var freelancer = await MilestoneWorkflowGuard.GetFreelancerContactAsync(
            _context, contract, cancellationToken);
        if (freelancer is null)
        {
            return;
        }

        var (deliveryType, deliveryKey, payload) = transition.MilestoneApproved
            ? ((int)DeliveryOutboxType.MilestoneAutoCompleted,
                $"milestone:{milestone.MilestonesId:D}:completed",
                (object)new MilestoneAutoCompletedDeliveryPayload(
                    contract.ContractsId,
                    milestone.MilestonesId,
                    milestone.Title,
                    transition.NextMilestone?.MilestonesId,
                    transition.NextMilestone?.Title,
                    freelancer.Value.Email,
                    freelancer.Value.FullName))
            : ((int)DeliveryOutboxType.WorkItemRevisionRequested,
                BuildRevisionDeliveryKey(milestone, reviewed),
                new WorkItemRevisionDeliveryPayload(
                    contract.ContractsId,
                    milestone.MilestonesId,
                    milestone.Title,
                    reviewed.Select(item => item.Title).ToList(),
                    command.Reason?.Trim() ?? string.Empty,
                    freelancer.Value.Email,
                    freelancer.Value.FullName));

        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = deliveryKey,
            ScheduleId = null,
            DeliveryType = deliveryType,
            RecipientUserId = freelancer.Value.UserId,
            EventSequence = 0,
            Channel = (int)DeliveryChannel.Email,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    /// <summary>
    /// Derived from the reviewed work items rather than the clock, so a retried request produces the
    /// same key and the outbox refuses the duplicate.
    /// </summary>
    private static string BuildRevisionDeliveryKey(Milestone milestone, List<ContractWorkItem> reviewed)
    {
        var ids = string.Join(
            ",",
            reviewed.Select(item => item.ContractWorkItemId.ToString("N")).OrderBy(id => id));

        return $"workitem:{milestone.MilestonesId:D}:revision:{ids}";
    }

    private async Task NotifyFreelancerAsync(
        Contract contract,
        Milestone milestone,
        List<ContractWorkItem> reviewed,
        ReviewContractWorkItemsCommand command,
        MilestoneTransition transition,
        CancellationToken cancellationToken)
    {
        if (_notificationService is null)
        {
            return;
        }

        try
        {
            var freelancer = await MilestoneWorkflowGuard.GetFreelancerContactAsync(
                _context, contract, cancellationToken);
            if (freelancer is null)
            {
                return;
            }

            var (title, body) = transition.MilestoneApproved
                ? ("Milestone completed",
                    transition.NextMilestone is null
                        ? $"{milestone.Title} is complete."
                        : $"{milestone.Title} is complete. Next up: {transition.NextMilestone.Title}.")
                : command.Approve
                    ? ("Work items approved", $"{reviewed.Count} work item(s) in {milestone.Title} were approved.")
                    : ("Revision requested", $"{reviewed.Count} work item(s) in {milestone.Title} need changes.");

            await _notificationService.CreateNotificationAsync(
                freelancer.Value.UserId,
                NotificationType.MilestoneUpdated,
                title,
                body,
                milestone.MilestonesId,
                nameof(Milestone),
                cancellationToken,
                SubmitContractWorkItemsCommandHandler.BuildActionUrlMetadata(
                    contract.ContractsId, milestone.MilestonesId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger?.LogError(
                exception,
                "Failed to notify the freelancer about work item review on milestone {MilestoneId}.",
                milestone.MilestonesId);
        }
    }

    private async Task PublishSystemMessageAsync(Message systemMessage)
    {
        try
        {
            await _realtimeNotifier!.SendConversationEventAsync(
                systemMessage.ConversationsId,
                "ReceiveMessage",
                ContractConversationEvents.ToRealtimePayload(systemMessage),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger?.LogWarning(exception, "Failed to publish the work item review system message.");
        }
    }

    private async Task<ContractMilestoneResponse> BuildResponseAsync(
        Contract contract,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var milestone = await _context.Set<Milestone>()
            .Include(item => item.MilestoneAttachments)
            .Include(item => item.WorkItems)
                .ThenInclude(workItem => workItem.Submissions)
                    .ThenInclude(submission => submission.Attachments)
            .AsSplitQuery()
            .FirstAsync(item => item.MilestonesId == milestoneId, cancellationToken);

        return MilestoneWorkflowGuard.ToResponse(milestone, contract.DeliveryMode);
    }
}
