using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using Domain.Enums.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.ReportContracts.Create.Commands;

public sealed class CreateReportCommandHandler :
    IRequestHandler<CreateReportCommand, ReportContractResponse>
{
    private const long MaxAttachmentFileSizeBytes = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly ILogger<CreateReportCommandHandler> _logger;
    private readonly IUserAuditLogService _userAuditLog;

    public CreateReportCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger<CreateReportCommandHandler> logger,
        IUserAuditLogService userAuditLog)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _logger = logger;
        _userAuditLog = userAuditLog;
    }

    public async Task<ReportContractResponse> Handle(
        CreateReportCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await ReportContractAccess.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        var participants = await ReportContractAccess.EnsureParticipantAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        // Cannot create reports on disputed contracts
        if (contract.Status == (int)ContractStatus.Disputed)
        {
            throw new BadRequestException("Cannot create a report while the contract is under dispute.");
        }

        // Determine respondent automatically
        var reporterRole = participants.GetRole(command.UserId);
        Guid? respondentId = reporterRole == "Client"
            ? participants.FreelancerUserId
            : participants.ClientUserId;

        if (respondentId is null)
        {
            throw new BadRequestException("Cannot determine the respondent for this report. The contract does not have a freelancer assigned.");
        }

        // Validate milestone if provided
        if (command.MilestoneId.HasValue)
        {
            var milestone = await _context.Set<Milestone>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MilestonesId == command.MilestoneId.Value, cancellationToken)
                ?? throw new NotFoundException("Milestone does not exist.");

            if (milestone.ContractsId != command.ContractId)
            {
                throw new BadRequestException("The specified milestone does not belong to this contract.");
            }
        }

        var now = _dateTimeService.UtcNow;
        var report = new ReportContract
        {
            ReportContractId = Guid.NewGuid(),
            ContractId = command.ContractId,
            ReporterId = command.UserId,
            RespondentId = respondentId,
            MilestoneId = command.MilestoneId,
            IssueType = command.IssueType,
            Description = command.Description.Trim(),
            DesiredResolution = command.DesiredResolution.Trim(),
            Status = (int)Domain.Enums.Reports.ContractReportStatus.Pending,
            ResolutionAction = null,
            Explanation = null,
            ProposedResolution = null,
            RejectReason = null,
            ResolvedBy = null,
            CreatedAt = now,
            RespondedAt = null,
            ResolvedAt = null,
            IsEscalatedToDispute = false
        };

        _context.Set<ReportContract>().Add(report);

        // Upload and save attachments
        var attachments = new List<ReportContractAttachment>();
        foreach (var attachment in command.Attachments)
        {
            ValidateAttachment(attachment);

            var safeFileName = Path.GetFileName(attachment.FileName.Trim());
            var fileUrl = await _mediaService.UploadFileAsync(
                attachment.Content,
                safeFileName,
                attachment.ContentType,
                "report-contract-attachments",
                cancellationToken);

            var reportAttachment = new ReportContractAttachment
            {
                ReportContractAttachmentId = Guid.NewGuid(),
                ReportContractId = report.ReportContractId,
                FileUrl = fileUrl,
                FileName = safeFileName,
                ContentType = attachment.ContentType,
                FileSize = attachment.Length,
                UploadedAt = now,
                UploadedByUserId = command.UserId
            };

            attachments.Add(reportAttachment);
            _context.Set<ReportContractAttachment>().Add(reportAttachment);
        }

        var reporter = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == command.UserId, cancellationToken);

        var systemMessage = await ReportContractSystemMessages.AddAsync(
            _context,
            report,
            ReportContractSystemMessages.CreatedEvent,
            reporter?.FullName,
            reporterRole,
            now,
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            reporterRole == "Client" ? UserRole.Client : UserRole.Freelancer,
            AuditUserActionType.ReportCreated,
            command.ContractId,
            $"Reported an issue: {command.IssueType}.",
            milestoneId: report.MilestoneId,
            reportId: report.ReportContractId);

        await _context.SaveChangesAsync(cancellationToken);

        if (systemMessage is not null)
        {
            await _chatRealtimeNotifier.SendConversationEventAsync(
                systemMessage.ConversationsId,
                "ReceiveMessage",
                ReportContractSystemMessages.ToRealtimePayload(systemMessage),
                cancellationToken);
        }

        // Notify the respondent
        if (respondentId.HasValue)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    respondentId.Value,
                    NotificationType.ReportUpdate,
                    "A new issue has been raised",
                    $"A new issue has been raised for contract '{contract.Title}'.",
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Report {ReportId} was created, but notification delivery to user {UserId} failed.",
                    report.ReportContractId,
                    respondentId.Value);
            }
        }

        string? milestoneTitle = null;
        if (command.MilestoneId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .AsNoTracking()
                .Where(m => m.MilestonesId == command.MilestoneId.Value)
                .Select(m => m.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var attachmentResponses = attachments
            .Select(a => new ReportContractAttachmentResponse(
                a.ReportContractAttachmentId,
                a.FileUrl,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.UploadedAt,
                a.UploadedByUserId))
            .ToList();

        return new ReportContractResponse(
            report.ReportContractId,
            report.ContractId,
            report.ReporterId,
            reporter?.FullName,
            reporterRole,
            report.RespondentId,
            null,
            participants.GetRole(respondentId!.Value),
            report.MilestoneId,
            milestoneTitle,
            report.IssueType,
            report.Description,
            report.DesiredResolution,
            report.Status,
            report.ResolutionAction,
            report.Explanation,
            report.ProposedResolution,
            report.RejectReason,
            report.ResolvedBy,
            report.CreatedAt,
            report.RespondedAt,
            report.ResolvedAt,
            report.IsEscalatedToDispute,
            attachmentResponses);
    }

    private static void ValidateAttachment(CreateReportFile file)
    {
        if (file.Length <= 0)
        {
            throw new BadRequestException("Attachment file is empty.");
        }

        if (file.Length > MaxAttachmentFileSizeBytes)
        {
            throw new BadRequestException("Attachment file size exceeds the maximum allowed size of 100 MB.");
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new BadRequestException("Attachment file name is required.");
        }

        var safeFileName = Path.GetFileName(file.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new BadRequestException("Attachment file name is invalid.");
        }

        if (safeFileName.Length > 500)
        {
            throw new BadRequestException("Attachment file name must not exceed 500 characters.");
        }
    }
}
