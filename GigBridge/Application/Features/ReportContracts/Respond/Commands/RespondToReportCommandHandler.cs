using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Internal;
using Application.Features.ReportContracts.Create.Commands;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.ReportContracts.Respond.Commands;

public sealed class RespondToReportCommandHandler :
    IRequestHandler<RespondToReportCommand, ReportContractResponse>
{
    private const long MaxAttachmentFileSizeBytes = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly ILogger<RespondToReportCommandHandler> _logger;

    public RespondToReportCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger<RespondToReportCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _logger = logger;
    }

    public async Task<ReportContractResponse> Handle(
        RespondToReportCommand command,
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

        var report = await _context.Set<ReportContract>()
            .Include(r => r.ReportContractAttachments)
            .FirstOrDefaultAsync(r => r.ReportContractId == command.ReportId, cancellationToken)
            ?? throw new NotFoundException("Report does not exist.");

        if (report.ContractId != command.ContractId)
        {
            throw new BadRequestException("The report does not belong to this contract.");
        }

        // Only the respondent can respond
        if (report.RespondentId != command.UserId)
        {
            throw new ForbiddenAccessException("Only the respondent can respond to this report.");
        }

        // Only pending reports can be responded to
        if (report.Status != (int)Domain.Enums.ContractReportStatus.Pending)
        {
            throw new BadRequestException("This report has already been responded to or is resolved.");
        }

        // Validate based on action type
        var actionType = (ContractReportResolutionAction)command.ResolutionAction;
        switch (actionType)
        {
            case ContractReportResolutionAction.RejectIssue:
                if (string.IsNullOrWhiteSpace(command.RejectReason))
                {
                    throw new BadRequestException("A reject reason is required when rejecting an issue.");
                }
                if (command.RejectReason.Trim().Length > 5000)
                {
                    throw new BadRequestException("Reject reason must not exceed 5000 characters.");
                }
                break;

            case ContractReportResolutionAction.ProvideExplanation:
                if (string.IsNullOrWhiteSpace(command.Explanation))
                {
                    throw new BadRequestException("Explanation is required when providing an explanation.");
                }
                if (command.Explanation.Trim().Length > 5000)
                {
                    throw new BadRequestException("Explanation must not exceed 5000 characters.");
                }
                break;

            case ContractReportResolutionAction.ProposeResolution:
                if (string.IsNullOrWhiteSpace(command.ProposedResolution))
                {
                    throw new BadRequestException("Proposed resolution is required when proposing a resolution.");
                }
                if (command.ProposedResolution.Trim().Length > 5000)
                {
                    throw new BadRequestException("Proposed resolution must not exceed 5000 characters.");
                }
                break;

            case ContractReportResolutionAction.AcceptIssue:
                break;

            default:
                throw new BadRequestException("Invalid resolution action.");
        }

        var now = _dateTimeService.UtcNow;
        report.ResolutionAction = command.ResolutionAction;
        report.Explanation = command.Explanation?.Trim();
        report.ProposedResolution = command.ProposedResolution?.Trim();
        report.RejectReason = command.RejectReason?.Trim();
        report.Status = (int)Domain.Enums.ContractReportStatus.WaitingReporterConfirmation;
        report.RespondedAt = now;
        report.UpdatedAt = now;

        // Upload and save respondent attachments
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

            _context.Set<ReportContractAttachment>().Add(reportAttachment);
        }

        var respondent = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == command.UserId, cancellationToken);

        var systemMessage = await ReportContractSystemMessages.AddAsync(
            _context,
            report,
            ReportContractSystemMessages.UpdatedEvent,
            respondent?.FullName,
            participants.GetRole(command.UserId),
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (systemMessage is not null)
        {
            await _chatRealtimeNotifier.SendConversationEventAsync(
                systemMessage.ConversationsId,
                "ReceiveMessage",
                ReportContractSystemMessages.ToRealtimePayload(systemMessage),
                cancellationToken);
        }

        // Notify the reporter
        var reporterId = report.ReporterId;
        try
        {
            await _notificationService.CreateNotificationAsync(
                reporterId,
                NotificationType.ReportUpdate,
                "A response has been submitted",
                $"A response to the issue on contract '{contract.Title}' has been submitted.",
                contract.ContractsId,
                nameof(Contract),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Report {ReportId} response was created, but notification delivery to user {UserId} failed.",
                report.ReportContractId,
                reporterId);
        }

        return await BuildResponse(report, contract, participants, cancellationToken);
    }

    private async Task<ReportContractResponse> BuildResponse(
        ReportContract report,
        Contract contract,
        ReportContractParticipants participants,
        CancellationToken cancellationToken)
    {
        var reporter = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == report.ReporterId, cancellationToken);

        User? respondent = null;
        if (report.RespondentId.HasValue)
        {
            respondent = await _context.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == report.RespondentId.Value, cancellationToken);
        }

        string? milestoneTitle = null;
        if (report.MilestoneId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .AsNoTracking()
                .Where(m => m.MilestonesId == report.MilestoneId.Value)
                .Select(m => m.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Reload attachments to include newly added respondent attachments
        var attachments = await _context.Set<ReportContractAttachment>()
            .Where(a => a.ReportContractId == report.ReportContractId)
            .OrderBy(a => a.UploadedAt)
            .Select(a => new ReportContractAttachmentResponse(
                a.ReportContractAttachmentId,
                string.Empty,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.UploadedAt,
                a.UploadedByUserId))
            .ToListAsync(cancellationToken);

        return new ReportContractResponse(
            report.ReportContractId,
            report.ContractId,
            report.ReporterId,
            reporter?.FullName,
            participants.GetRole(report.ReporterId),
            report.RespondentId,
            respondent?.FullName,
            report.RespondentId.HasValue ? participants.GetRole(report.RespondentId.Value) : null,
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
            attachments);
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
