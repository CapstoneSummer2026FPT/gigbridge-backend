using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Files;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.Models.Files;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Delivery;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed class SubmitMilestoneCommandHandler :
    IRequestHandler<SubmitMilestoneCommand, ContractMilestoneResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly IWorkspaceUploadFilePolicy _uploadFilePolicy;
    private readonly IMediaService? _mediaService;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;
    private readonly ILogger<SubmitMilestoneCommandHandler>? _logger;

    public SubmitMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserAuditLogService userAuditLog,
        IWorkspaceUploadFilePolicy uploadFilePolicy,
        IMediaService? mediaService = null,
        IChatRealtimeNotifier? realtimeNotifier = null,
        ILogger<SubmitMilestoneCommandHandler>? logger = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userAuditLog = userAuditLog;
        _uploadFilePolicy = uploadFilePolicy;
        _mediaService = mediaService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<ContractMilestoneResponse> Handle(
        SubmitMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);
        MilestoneDeliveryModeGuard.EnsureLegacySubmission(contract);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.InProgress)
        {
            throw new BadRequestException("Only in-progress milestones can be submitted.");
        }

        var workItems = await _context.Set<ContractWorkItem>()
            .Where(item => item.MilestonesId == milestone.MilestonesId)
            .ToListAsync(cancellationToken);
        if (workItems.Count == 0 || workItems.Any(item => !ContractWorkItemStatusExtensions.IsDelivered(item.Status)))
        {
            throw new BadRequestException("All milestone work items must be completed before submitting deliverables.");
        }

        var validatedFiles = await ValidateRequestAsync(command, cancellationToken);
        try
        {
            if (_mediaService is null)
            {
                throw new InvalidOperationException("MediaService is not configured for file uploads.");
            }

            var now = _dateTimeService.UtcNow;
            // Scoped to milestone-level attachments on purpose. Files belonging to a work item
            // submission attempt (ContractWorkItemSubmissionId set) are an append-only delivery
            // history and the evidence an admin reads in a dispute — this handler replaces only
            // its own bundle and must never RemoveRange them.
            var existingAttachments = await _context.Set<MilestoneAttachment>()
                .Where(attachment => attachment.MilestonesId == milestone.MilestonesId &&
                                     attachment.ContractWorkItemSubmissionId == null)
                .ToListAsync(cancellationToken);
            var newAttachments = new List<MilestoneAttachment>(validatedFiles.Count);

            try
            {
                foreach (var validatedFile in validatedFiles)
                {
                    newAttachments.Add(await CreateAttachmentAsync(
                        command.UserId,
                        validatedFile,
                        milestone.MilestonesId,
                        now,
                        cancellationToken));
                }
            }
            catch
            {
                await DeleteAttachmentsBestEffortAsync(newAttachments, CancellationToken.None);
                throw;
            }

            Message? systemMessage;
            try
            {
                await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

                if (existingAttachments.Count > 0)
                {
                    _context.Set<MilestoneAttachment>().RemoveRange(existingAttachments);
                }

                _context.Set<MilestoneAttachment>().AddRange(newAttachments);

                // Same reasoning as above: Clear() would drop work item submission files from the
                // tracked graph too, so remove only the milestone-level set being replaced.
                foreach (var attachment in existingAttachments)
                {
                    milestone.MilestoneAttachments.Remove(attachment);
                }

                foreach (var attachment in newAttachments)
                {
                    milestone.MilestoneAttachments.Add(attachment);
                }

                milestone.SubmissionDescription = NormalizeDescription(command.Description);
                milestone.Status = (int)MilestoneStatus.Submitted;
                milestone.SubmittedAt = now;
                milestone.UpdatedAt = now;
                contract.UpdatedAt = now;

                systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
                    _context,
                    contract.ContractsId,
                    $"Milestone submitted: {milestone.Title}.",
                    now,
                    cancellationToken);

                _userAuditLog.Add(
                    command.UserId,
                    UserRole.Freelancer,
                    AuditUserActionType.MilestoneSubmitted,
                    contract.ContractsId,
                    $"Submitted milestone: {milestone.Title}.",
                    milestoneId: milestone.MilestonesId);

                await EnqueueSubmissionEmailAsync(contract, milestone, now, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await DeleteAttachmentsBestEffortAsync(newAttachments, CancellationToken.None);
                throw;
            }

            await DeleteAttachmentsBestEffortAsync(existingAttachments, CancellationToken.None);

            if (_realtimeNotifier is not null)
            {
                var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(
                    _context,
                    contract,
                    cancellationToken);
                await _realtimeNotifier.SendUsersEventAsync(
                    participantIds,
                    "DeliverableSubmitted",
                    new
                    {
                        contractId = contract.ContractsId,
                        milestoneId = milestone.MilestonesId,
                        status = milestone.Status
                    },
                    cancellationToken);
                if (systemMessage is not null)
                {
                    await _realtimeNotifier.SendConversationEventAsync(
                        systemMessage.ConversationsId,
                        "ReceiveMessage",
                        ContractConversationEvents.ToRealtimePayload(systemMessage),
                        cancellationToken);
                }
            }

            return MilestoneWorkflowGuard.ToResponse(milestone, contract.DeliveryMode);
        }
        finally
        {
            await validatedFiles.DisposeAsync();
        }
    }

    private async Task<ValidatedWorkspaceUploadBatch> ValidateRequestAsync(
        SubmitMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Files.Count == 0)
        {
            throw new BadRequestException("A milestone deliverable file is required.");
        }

        if (command.Description is not null && command.Description.Length > 5000)
        {
            throw new BadRequestException("Submission description exceeds 5000 characters.");
        }

        return await _uploadFilePolicy.ValidateBatchAsync(
            command.Files
                .Select(file => new WorkspaceUploadFile(
                    file.Content,
                    file.FileName,
                    file.ContentType,
                    file.Length))
                .ToList(),
            WorkspaceUploadLimits.MaxFilesPerBatch,
            cancellationToken);
    }

    private async Task<MilestoneAttachment> CreateAttachmentAsync(
        Guid uploadedByUserId,
        ValidatedWorkspaceUploadFile file,
        Guid milestoneId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var fileUrl = await _mediaService!.UploadFileAsync(
            file.Content,
            file.FileName,
            file.ContentType,
            "milestones",
            cancellationToken);

        return new MilestoneAttachment
        {
            MilestoneAttachmentsId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            FileName = file.FileName.Trim(),
            FileUrl = fileUrl,
            FileSize = file.Length,
            SourceType = (int)MilestoneSubmissionSourceType.File,
            MimeType = string.IsNullOrWhiteSpace(file.ContentType)
                ? null
                : file.ContentType.Trim(),
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
                await _mediaService.DeleteFileAsync(
                    attachment.FileUrl,
                    "milestones",
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger?.LogWarning(
                    exception,
                    "Failed to clean up milestone attachment {AttachmentId} at {FileUrl}.",
                    attachment.MilestoneAttachmentsId,
                    attachment.FileUrl);
            }
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private async Task EnqueueSubmissionEmailAsync(
        Contract contract,
        Milestone milestone,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var client = await MilestoneWorkflowGuard.GetClientContactAsync(_context, contract, cancellationToken);

        var deliveryKey = $"milestone:{milestone.MilestonesId:D}:submitted:{now:O}";
        var payload = new MilestoneSubmissionDeliveryPayload(
            contract.ContractsId,
            milestone.MilestonesId,
            client.Email,
            client.FullName);

        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = deliveryKey,
            ScheduleId = null,
            DeliveryType = (int)DeliveryOutboxType.MilestoneSubmission,
            RecipientUserId = client.UserId,
            EventSequence = 0,
            Channel = (int)DeliveryChannel.Email,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }
}
