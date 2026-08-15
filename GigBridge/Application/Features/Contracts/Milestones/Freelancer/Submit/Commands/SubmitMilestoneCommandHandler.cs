using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
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

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed class SubmitMilestoneCommandHandler :
    IRequestHandler<SubmitMilestoneCommand, ContractMilestoneResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly IMediaService? _mediaService;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public SubmitMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserAuditLogService userAuditLog,
        IMediaService? mediaService = null,
        IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userAuditLog = userAuditLog;
        _mediaService = mediaService;
        _realtimeNotifier = realtimeNotifier;
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

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.InProgress && milestone.Status != (int)MilestoneStatus.Pending)
        {
            throw new BadRequestException("Only in-progress or pending milestones can be submitted.");
        }

        var workItems = await _context.Set<ContractWorkItem>()
            .Where(item => item.MilestonesId == milestone.MilestonesId)
            .ToListAsync(cancellationToken);
        if (workItems.Count == 0 || workItems.Any(item => item.Status != (int)ContractWorkItemStatus.Completed))
        {
            throw new BadRequestException("All milestone work items must be completed before submitting deliverables.");
        }

        var validatedFile = await ValidateRequestAsync(command, cancellationToken);

        var now = _dateTimeService.UtcNow;
        var existingAttachments = await _context.Set<MilestoneAttachment>()
            .Where(attachment => attachment.MilestonesId == milestone.MilestonesId)
            .ToListAsync(cancellationToken);

        if (existingAttachments.Count > 0)
        {
            _context.Set<MilestoneAttachment>().RemoveRange(existingAttachments);
            milestone.MilestoneAttachments.Clear();
        }

        var attachment = await CreateAttachmentAsync(
            command,
            validatedFile,
            milestone.MilestonesId,
            now,
            cancellationToken);
        _context.Set<MilestoneAttachment>().Add(attachment);
        milestone.MilestoneAttachments.Add(attachment);

        milestone.SubmissionDescription = NormalizeDescription(command.Description);
        milestone.Status = (int)MilestoneStatus.Submitted;
        milestone.SubmittedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;

        var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
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

        if (_realtimeNotifier is not null)
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(_context, contract, cancellationToken);
            await _realtimeNotifier.SendUsersEventAsync(
                participantIds,
                "DeliverableSubmitted",
                new { contractId = contract.ContractsId, milestoneId = milestone.MilestonesId, status = milestone.Status },
                cancellationToken);
            if (systemMessage is not null)
                await _realtimeNotifier.SendConversationEventAsync(
                    systemMessage.ConversationsId, "ReceiveMessage",
                    ContractConversationEvents.ToRealtimePayload(systemMessage), cancellationToken);
        }

        return MilestoneWorkflowGuard.ToResponse(milestone);
    }

    private static async Task<ValidatedMilestoneSubmissionFile> ValidateRequestAsync(
        SubmitMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        if (command.File is null)
        {
            throw new BadRequestException("A milestone deliverable file is required.");
        }

        if (command.Description is not null && command.Description.Length > 5000)
        {
            throw new BadRequestException("Submission description exceeds 5000 characters.");
        }

        return await MilestoneSubmissionFilePolicy.ValidateAsync(
            command.File,
            cancellationToken);
    }

    private async Task<MilestoneAttachment> CreateAttachmentAsync(
        SubmitMilestoneCommand command,
        ValidatedMilestoneSubmissionFile file,
        Guid milestoneId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (_mediaService == null)
        {
            throw new InvalidOperationException("MediaService is not configured for file uploads.");
        }

        var fileUrl = await _mediaService.UploadFileAsync(
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
            UploadedByUserId = command.UserId,
            CreatedAt = now
        };
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
