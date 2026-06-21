using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.ResolveReport.Commands;

public class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand>
{
    private const int CancelledJobPostStatus = 3;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ResolveReportCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
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

        if (command.Request.TakeAction)
        {
            await ApplyModerationActionAsync(report, cancellationToken);
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

    private async Task ApplyModerationActionAsync(Report report, CancellationToken cancellationToken)
    {
        switch (report.ReportedEntityType)
        {
            case ReportedEntityTypes.User:
                var user = await _context.Set<User>()
                    .FirstOrDefaultAsync(item => item.UserId == report.ReportedEntityId, cancellationToken);
                if (user is null)
                {
                    throw new NotFoundException("Reported user does not exist.");
                }
                user.IsActive = false;
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
                var review = await _context.Set<Review>()
                    .FirstOrDefaultAsync(item => item.ReviewsId == report.ReportedEntityId, cancellationToken);
                if (review is null)
                {
                    throw new NotFoundException("Reported review does not exist.");
                }
                review.IsVisible = false;
                review.UpdatedAt = _dateTimeService.UtcNow;
                break;

            default:
                throw new BadRequestException("Unsupported reported entity type.");
        }
    }
}
