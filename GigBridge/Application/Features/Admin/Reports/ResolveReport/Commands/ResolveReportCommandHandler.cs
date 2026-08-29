using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Reviews.Interfaces;
using Application.Common.InternalServices.Reviews.Models;
using Application.Common.InternalServices.Reviews.Services;
using Application.Features.Admin.Reports.AccountReports;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Reports;
using Domain.Enums.Reviews;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.ResolveReport.Commands;

public class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand>
{
    private const int CancelledJobPostStatus = 3;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IReviewModerationService _reviewModerationService;
    private readonly IMediator? _mediator;

    public ResolveReportCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IReviewModerationService reviewModerationService,
        IMediator? mediator = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _reviewModerationService = reviewModerationService;
        _mediator = mediator;
    }

    public async Task Handle(ResolveReportCommand command, CancellationToken cancellationToken)
    {
        var adminExists = await _context.Set<User>()
            .AnyAsync(user => user.UserId == command.AdminId && user.Role == (int)UserRole.Admin, cancellationToken);

        if (!adminExists)
        {
            throw new NotFoundException("Admin user does not exist.");
        }

        var report = await _context.Set<Report>()
            .FirstOrDefaultAsync(item => item.ReportsId == command.ReportId, cancellationToken);

        if (report is null)
        {
            throw new NotFoundException("Report does not exist.");
        }

        if (report.Status is (int)ReportStatus.Resolved or (int)ReportStatus.Dismissed)
        {
            throw new BadRequestException("Resolved or dismissed reports cannot be resolved again.");
        }

        if (report.ReportedEntityType == ReportedEntityTypes.User && _mediator is not null)
        {
            await _mediator.Send(new ResolveAccountReportCommand(command.AdminId, command.ReportId,
                new ResolveAccountReportRequest(
                    command.Request.TakeAction ? AccountReportResolutionAction.PermanentBan : AccountReportResolutionAction.None,
                    command.Request.TakeAction ? UserViolationType.PlatformPolicyViolation : null,
                    command.Request.AdminNote ?? report.Reason,
                    report.Description,
                    command.Request.AdminNote,
                    null)), cancellationToken);
            return;
        }

        if (command.Request.TakeAction)
        {
            await ApplyModerationActionAsync(
                report,
                command.AdminId,
                command.Request.AdminNote ?? report.Reason,
                cancellationToken);
        }

        var now = _dateTimeService.UtcNow;
        report.Status = (int)ReportStatus.Resolved;
        report.AdminNote = string.IsNullOrWhiteSpace(command.Request.AdminNote)
            ? report.AdminNote
            : command.Request.AdminNote.Trim();
        report.ResolvedByAdminId = command.AdminId;
        report.ResolvedAt = now;
        report.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyModerationActionAsync(
        Report report,
        Guid adminId,
        string moderationNote,
        CancellationToken cancellationToken)
    {
        switch (report.ReportedEntityType)
        {
            case ReportedEntityTypes.User:
                var user = await _context.Set<User>().FirstOrDefaultAsync(item => item.UserId == report.ReportedEntityId, cancellationToken)
                    ?? throw new NotFoundException("Reported user does not exist.");
                user.AccountStatus = (int)AccountStatus.Banned;
                user.IsActive = false;
                user.BannedAt = _dateTimeService.UtcNow;
                user.BanReason = moderationNote;
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiry = null;
                user.PreviousRefreshTokenHash = null;
                user.PreviousRefreshTokenGraceExpiresAt = null;
                user.UpdatedAt = _dateTimeService.UtcNow;
                break;

            case ReportedEntityTypes.JobPost:
                var jobPost = await _context.Set<JobPost>()
                    .FirstOrDefaultAsync(item => item.JobPostsId == report.ReportedEntityId, cancellationToken);
                if (jobPost is null)
                {
                    throw new NotFoundException("Reported job post does not exist.");
                }
                jobPost.Status = CancelledJobPostStatus;
                jobPost.UpdatedAt = _dateTimeService.UtcNow;
                break;

            case ReportedEntityTypes.Review:
                await _reviewModerationService.SetStatusAsync(
                    report.ReportedEntityId,
                    ReviewModerationStatus.Hidden,
                    adminId,
                    moderationNote,
                    cancellationToken);
                break;

            default:
                throw new BadRequestException("Unsupported reported entity type.");
        }
    }
}
