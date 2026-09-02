using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Files;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.Models.Files;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Contracts.Milestones.WorkItems.Common;
using Application.Features.Contracts.Milestones.WorkItems.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Delivery;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Milestones.WorkItems.Freelancer.Submit.Commands;

/// <summary>
/// Submits deliverables for one or more work items in a single atomic batch.
///
/// All-or-nothing on purpose: the freelancer picks several work items, presses submit once, and
/// either every attempt is recorded with its files or none is. A partial commit would leave the
/// milestone half-submitted with orphaned uploads and no way for either party to tell what landed.
/// </summary>
public sealed class SubmitContractWorkItemsCommandHandler :
    IRequestHandler<SubmitContractWorkItemsCommand, ContractMilestoneResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly IWorkspaceUploadFilePolicy _uploadFilePolicy;
    private readonly IMediaService? _mediaService;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<SubmitContractWorkItemsCommandHandler>? _logger;

    public SubmitContractWorkItemsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserAuditLogService userAuditLog,
        IWorkspaceUploadFilePolicy uploadFilePolicy,
        IMediaService? mediaService = null,
        IChatRealtimeNotifier? realtimeNotifier = null,
        INotificationService? notificationService = null,
        ILogger<SubmitContractWorkItemsCommandHandler>? logger = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userAuditLog = userAuditLog;
        _uploadFilePolicy = uploadFilePolicy;
        _mediaService = mediaService;
        _realtimeNotifier = realtimeNotifier;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ContractMilestoneResponse> Handle(
        SubmitContractWorkItemsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(
            _context, contract, command.UserId, cancellationToken);
        MilestoneDeliveryModeGuard.EnsureWorkItemDelivery(contract);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context, command.ContractId, command.MilestoneId, cancellationToken);

        ValidateBatchShape(command);

        var workItems = await LoadWorkItemsAsync(milestone.MilestonesId, cancellationToken);
        var targeted = ResolveTargets(command, workItems);

        // Idempotency: the browser retried the same batch. Return the committed state rather than
        // recording a second revision for work the freelancer only submitted once.
        if (await IsAlreadySubmittedAsync(command, targeted, cancellationToken))
        {
            return await BuildResponseAsync(contract, milestone.MilestonesId, cancellationToken);
        }

        foreach (var item in targeted)
        {
            if (!ContractWorkItemStatusExtensions.CanSubmit(item.Status))
            {
                throw new BadRequestException(
                    $"Work item {item.Title} cannot be submitted while it is {(ContractWorkItemStatus)item.Status}.");
            }
        }

        await using var validatedFiles = await ValidateFilesAsync(command, cancellationToken);

        if (_mediaService is null)
        {
            throw new InvalidOperationException("MediaService is not configured for file uploads.");
        }

        var now = _dateTimeService.UtcNow;
        var uploaded = new List<MilestoneAttachment>();

        try
        {
            var attempts = await BuildAttemptsAsync(
                command, targeted, validatedFiles, milestone.MilestonesId, now, uploaded, cancellationToken);

            MilestoneTransition transition;
            Message? systemMessage;

            await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
            {
                // Serialize with any other batch on this milestone. Without it two concurrent
                // submissions both read the pre-batch item set and neither notices that the
                // milestone has just become fully submitted.
                await transaction.AcquireTransactionLockAsync(
                    MilestoneDeliveryLock.ForMilestone(milestone.MilestonesId),
                    cancellationToken,
                    lockPurpose: "work-item-submission");

                // Re-read after the lock: the set validated above may be stale by now.
                var freshItems = await LoadWorkItemsAsync(milestone.MilestonesId, cancellationToken);

                _context.Set<ContractWorkItemSubmission>().AddRange(attempts);
                _context.Set<MilestoneAttachment>().AddRange(uploaded);

                foreach (var attempt in attempts)
                {
                    var item = freshItems.First(candidate =>
                        candidate.ContractWorkItemId == attempt.ContractWorkItemId);
                    item.Status = (int)ContractWorkItemStatus.Submitted;
                    item.CompletedAt = null;
                    item.UpdatedAt = now;
                }

                var orderedMilestones = await MilestoneWorkflowGuard.OrderMilestones(
                        _context.Set<Milestone>().Where(item => item.ContractsId == contract.ContractsId))
                    .ToListAsync(cancellationToken);

                var hasApprovedEarlyStart = await _context.Set<MilestoneEarlyStartRequest>().AnyAsync(
                    request => request.MilestonesId == milestone.MilestonesId &&
                               request.Status == (int)MilestoneEarlyStartRequestStatus.Approved,
                    cancellationToken);

                MilestoneWorkItemWorkflow.TryStart(milestone, orderedMilestones, hasApprovedEarlyStart, now);
                transition = MilestoneWorkItemWorkflow.ApplyAfterSubmit(milestone, freshItems, now);

                contract.UpdatedAt = now;

                systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
                    _context,
                    contract.ContractsId,
                    BuildSystemMessage(milestone, targeted),
                    now,
                    cancellationToken);

                _userAuditLog.Add(
                    command.UserId,
                    UserRole.Freelancer,
                    AuditUserActionType.MilestoneSubmitted,
                    contract.ContractsId,
                    $"Submitted {targeted.Count} work item(s) for milestone: {milestone.Title}.",
                    milestoneId: milestone.MilestonesId);

                await EnqueueSubmissionEmailAsync(contract, milestone, targeted, command, now, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            await NotifyClientAsync(contract, milestone, targeted, CancellationToken.None);

            await MilestoneWorkItemRealtimeEvents.PublishWorkItemSubmittedAsync(
                _context,
                _realtimeNotifier,
                _logger,
                contract,
                milestone,
                targeted.Select(item => item.ContractWorkItemId).ToList(),
                command.SubmissionBatchId,
                transition.MilestoneSubmitted,
                CancellationToken.None);

            if (systemMessage is not null && _realtimeNotifier is not null)
            {
                await PublishSystemMessageAsync(systemMessage);
            }

            return await BuildResponseAsync(contract, milestone.MilestonesId, CancellationToken.None);
        }
        catch
        {
            // Anything failing after an upload leaves orphaned media behind, so clean up whatever
            // this request put in storage before letting the error surface.
            await DeleteAttachmentsBestEffortAsync(uploaded, CancellationToken.None);
            throw;
        }
    }

    private static void ValidateBatchShape(SubmitContractWorkItemsCommand command)
    {
        if (command.SubmissionBatchId == Guid.Empty)
        {
            throw new BadRequestException("A submission batch id is required.");
        }

        if (command.Items.Count == 0)
        {
            throw new BadRequestException("Select at least one work item to submit.");
        }

        if (command.Items.Select(item => item.WorkItemId).Distinct().Count() != command.Items.Count)
        {
            throw new BadRequestException("The same work item cannot appear twice in one submission.");
        }

        foreach (var item in command.Items)
        {
            // Uploading a file IS the submission — there is no separate "mark done" step any more.
            if (item.Files.Count == 0)
            {
                throw new BadRequestException("Each selected work item needs at least one deliverable file.");
            }

            if (item.Note is { Length: > 5000 })
            {
                throw new BadRequestException("A work item note exceeds 5000 characters.");
            }
        }
    }

    private async Task<List<ContractWorkItem>> LoadWorkItemsAsync(
        Guid milestoneId,
        CancellationToken cancellationToken) =>
        await _context.Set<ContractWorkItem>()
            .Where(item => item.MilestonesId == milestoneId)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);

    private static List<ContractWorkItem> ResolveTargets(
        SubmitContractWorkItemsCommand command,
        List<ContractWorkItem> workItems)
    {
        var byId = workItems.ToDictionary(item => item.ContractWorkItemId);
        var targeted = new List<ContractWorkItem>(command.Items.Count);

        foreach (var entry in command.Items)
        {
            if (!byId.TryGetValue(entry.WorkItemId, out var item))
            {
                throw new NotFoundException("A selected work item does not belong to this milestone.");
            }

            targeted.Add(item);
        }

        return targeted;
    }

    private async Task<bool> IsAlreadySubmittedAsync(
        SubmitContractWorkItemsCommand command,
        List<ContractWorkItem> targeted,
        CancellationToken cancellationToken)
    {
        var ids = targeted.Select(item => item.ContractWorkItemId).ToList();

        return await _context.Set<ContractWorkItemSubmission>().AnyAsync(
            submission => submission.SubmissionBatchId == command.SubmissionBatchId &&
                          ids.Contains(submission.ContractWorkItemId),
            cancellationToken);
    }

    private async Task<ValidatedWorkspaceUploadBatch> ValidateFilesAsync(
        SubmitContractWorkItemsCommand command,
        CancellationToken cancellationToken)
    {
        // Validated as one batch so the shared per-file and per-batch limits apply to what the
        // request actually uploads, rather than to each work item in isolation.
        var files = command.Items
            .SelectMany(entry => entry.Files)
            .Select(file => new WorkspaceUploadFile(file.Content, file.FileName, file.ContentType, file.Length))
            .ToList();

        return await _uploadFilePolicy.ValidateBatchAsync(
            files, WorkspaceUploadLimits.MaxFilesPerBatch, cancellationToken);
    }

    private async Task<List<ContractWorkItemSubmission>> BuildAttemptsAsync(
        SubmitContractWorkItemsCommand command,
        List<ContractWorkItem> targeted,
        ValidatedWorkspaceUploadBatch validatedFiles,
        Guid milestoneId,
        DateTime now,
        List<MilestoneAttachment> uploaded,
        CancellationToken cancellationToken)
    {
        var nextRevisions = await NextRevisionNumbersAsync(targeted, cancellationToken);
        var attempts = new List<ContractWorkItemSubmission>(command.Items.Count);
        var fileCursor = 0;

        foreach (var entry in command.Items)
        {
            var attempt = new ContractWorkItemSubmission
            {
                ContractWorkItemSubmissionId = Guid.NewGuid(),
                ContractWorkItemId = entry.WorkItemId,
                RevisionNumber = nextRevisions[entry.WorkItemId],
                SubmissionBatchId = command.SubmissionBatchId,
                Note = string.IsNullOrWhiteSpace(entry.Note) ? null : entry.Note.Trim(),
                SubmittedAt = now,
                SubmittedByUserId = command.UserId,
                ReviewStatus = (int)ContractWorkItemSubmissionReviewStatus.Submitted
            };

            // ValidatedWorkspaceUploadBatch preserves the order the files were flattened in, so the
            // cursor walks each entry's slice back onto its own attempt.
            for (var fileIndex = 0; fileIndex < entry.Files.Count; fileIndex++)
            {
                var validated = validatedFiles[fileCursor++];
                uploaded.Add(await CreateAttachmentAsync(
                    command.UserId,
                    validated,
                    milestoneId,
                    attempt.ContractWorkItemSubmissionId,
                    now,
                    cancellationToken));
            }

            attempts.Add(attempt);
        }

        return attempts;
    }

    private async Task<Dictionary<Guid, int>> NextRevisionNumbersAsync(
        List<ContractWorkItem> targeted,
        CancellationToken cancellationToken)
    {
        var ids = targeted.Select(item => item.ContractWorkItemId).ToList();

        var existing = await _context.Set<ContractWorkItemSubmission>()
            .Where(submission => ids.Contains(submission.ContractWorkItemId))
            .ToListAsync(cancellationToken);

        return ids.ToDictionary(
            id => id,
            id => existing.Where(submission => submission.ContractWorkItemId == id)
                      .Select(submission => submission.RevisionNumber)
                      .DefaultIfEmpty(0)
                      .Max() + 1);
    }

    private async Task<MilestoneAttachment> CreateAttachmentAsync(
        Guid uploadedByUserId,
        ValidatedWorkspaceUploadFile file,
        Guid milestoneId,
        Guid submissionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var fileUrl = await _mediaService!.UploadFileAsync(
            file.Content, file.FileName, file.ContentType, "milestones", cancellationToken);

        return new MilestoneAttachment
        {
            MilestoneAttachmentsId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            ContractWorkItemSubmissionId = submissionId,
            FileName = file.FileName.Trim(),
            FileUrl = fileUrl,
            FileSize = file.Length,
            SourceType = (int)MilestoneSubmissionSourceType.File,
            MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType.Trim(),
            UploadedByUserId = uploadedByUserId,
            CreatedAt = now
        };
    }

    private async Task DeleteAttachmentsBestEffortAsync(
        IEnumerable<MilestoneAttachment> attachments,
        CancellationToken cancellationToken)
    {
        if (_mediaService is null)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            try
            {
                await _mediaService.DeleteFileAsync(attachment.FileUrl, "milestones", cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger?.LogWarning(
                    exception,
                    "Failed to clean up work item attachment {AttachmentId} at {FileUrl}.",
                    attachment.MilestoneAttachmentsId,
                    attachment.FileUrl);
            }
        }
    }

    private static string BuildSystemMessage(Milestone milestone, List<ContractWorkItem> targeted) =>
        targeted.Count == 1
            ? $"Work item submitted: {targeted[0].Title} ({milestone.Title})."
            : $"{targeted.Count} work items submitted for milestone: {milestone.Title}.";

    private async Task EnqueueSubmissionEmailAsync(
        Contract contract,
        Milestone milestone,
        List<ContractWorkItem> targeted,
        SubmitContractWorkItemsCommand command,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var client = await MilestoneWorkflowGuard.GetClientContactAsync(_context, contract, cancellationToken);

        var payload = new WorkItemSubmissionDeliveryPayload(
            contract.ContractsId,
            milestone.MilestonesId,
            milestone.Title,
            targeted.Select(item => item.Title).ToList(),
            client.Email,
            client.FullName);

        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            // Keyed on the batch, not the clock: a retried request reuses the key so the outbox
            // cannot send a second email for one submission.
            DeliveryKey = $"workitem:{milestone.MilestonesId:D}:submitted:{command.SubmissionBatchId:D}",
            ScheduleId = null,
            DeliveryType = (int)DeliveryOutboxType.WorkItemSubmission,
            RecipientUserId = client.UserId,
            EventSequence = 0,
            Channel = (int)DeliveryChannel.Email,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    private async Task NotifyClientAsync(
        Contract contract,
        Milestone milestone,
        List<ContractWorkItem> targeted,
        CancellationToken cancellationToken)
    {
        if (_notificationService is null)
        {
            return;
        }

        try
        {
            var client = await MilestoneWorkflowGuard.GetClientContactAsync(_context, contract, cancellationToken);

            await _notificationService.CreateNotificationAsync(
                client.UserId,
                NotificationType.MilestoneUpdated,
                "Deliverables submitted",
                targeted.Count == 1
                    ? $"{targeted[0].Title} is ready for your review."
                    : $"{targeted.Count} work items in {milestone.Title} are ready for your review.",
                milestone.MilestonesId,
                nameof(Milestone),
                cancellationToken,
                BuildActionUrlMetadata(contract.ContractsId, milestone.MilestonesId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger?.LogError(
                exception,
                "Failed to notify the client about work item submission on milestone {MilestoneId}.",
                milestone.MilestonesId);
        }
    }

    /// <summary>
    /// Deep-links the notification straight at the milestone. getActionUrl honours actionUrl first,
    /// so the client never has to guess which milestone the notification meant.
    /// </summary>
    internal static string BuildActionUrlMetadata(Guid contractId, Guid milestoneId) =>
        JsonSerializer.Serialize(
            new { actionUrl = $"/deliveryspace/{contractId:D}/milestones/{milestoneId:D}" },
            JsonOptions);

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
            _logger?.LogWarning(exception, "Failed to publish the work item submission system message.");
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
