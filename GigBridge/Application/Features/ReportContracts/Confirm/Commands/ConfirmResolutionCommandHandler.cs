using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.ReportContracts.Confirm.Commands;

public sealed class ConfirmResolutionCommandHandler :
    IRequestHandler<ConfirmResolutionCommand, ReportContractResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly ILogger<ConfirmResolutionCommandHandler> _logger;

    public ConfirmResolutionCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger<ConfirmResolutionCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _logger = logger;
    }

    public async Task<ReportContractResponse> Handle(
        ConfirmResolutionCommand command,
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

        // Only the reporter can confirm or decline
        if (report.ReporterId != command.UserId)
        {
            throw new ForbiddenAccessException("Only the reporter can confirm or decline the resolution.");
        }

        // Only reports waiting for confirmation can be acted upon
        if (report.Status != (int)Domain.Enums.ContractReportStatus.WaitingReporterConfirmation)
        {
            throw new BadRequestException("This report is not waiting for confirmation.");
        }

        var now = _dateTimeService.UtcNow;

        if (command.IsAccepted)
        {
            // Accept the resolution
            report.Status = (int)Domain.Enums.ContractReportStatus.Resolved;
            report.ResolvedBy = command.UserId;
            report.ResolvedAt = now;
            report.UpdatedAt = now;
            report.AdminReviewStatus = (int)ContractReportAdminStatus.Closed;
            report.AdminResolutionAction = (int)ContractReportAdminResolutionAction.ResolvedByParties;
            report.AdminResolutionNote = "Resolution accepted by the reporter.";

            var reporter = await _context.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == command.UserId, cancellationToken);

            var systemMessage = await ReportContractSystemMessages.AddAsync(
                _context,
                report,
                ReportContractSystemMessages.ResolvedEvent,
                reporter?.FullName,
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

            // Notify the respondent
            if (report.RespondentId.HasValue)
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        report.RespondentId.Value,
                        NotificationType.ReportUpdate,
                        "Issue has been resolved",
                        $"The issue on contract '{contract.Title}' has been resolved.",
                        contract.ContractsId,
                        nameof(Contract),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "Report {ReportId} resolution confirmation failed to notify user {UserId}.",
                        report.ReportContractId,
                        report.RespondentId.Value);
                }
            }
        }
        else
        {
            // Decline - keep in WaitingReporterConfirmation for now
            // TODO:
            // Future: Convert Report into Dispute.
            // Future: Assign Admin.
            // Future: Lock Contract.

            report.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
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

        var attachments = report.ReportContractAttachments
            .OrderBy(a => a.UploadedAt)
            .Select(a => new ReportContractAttachmentResponse(
                a.ReportContractAttachmentId,
                string.Empty,
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
}
