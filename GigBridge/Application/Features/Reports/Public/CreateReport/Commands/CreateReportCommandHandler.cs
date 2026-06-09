using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Public.CreateReport.Commands;

public class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateReportCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> Handle(CreateReportCommand command, CancellationToken cancellationToken)
    {
        var reporterExists = await _context.Set<User>()
            .AnyAsync(user => user.UserId == command.ReporterId, cancellationToken);

        if (!reporterExists)
        {
            throw new NotFoundException("Reporter does not exist.");
        }

        var entityType = ReportedEntityTypes.Normalize(command.Request.ReportedEntityType);
        await EnsureTargetCanBeReportedAsync(entityType, command.Request.ReportedEntityId, command.ReporterId, cancellationToken);

        var hasOpenDuplicate = await _context.Set<Report>()
            .AnyAsync(report =>
                report.ReporterId == command.ReporterId &&
                report.ReportedEntityId == command.Request.ReportedEntityId &&
                report.ReportedEntityType == entityType &&
                (report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing),
                cancellationToken);

        if (hasOpenDuplicate)
        {
            throw new ConflictException("You already have an open report for this target.");
        }

        var report = new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = command.ReporterId,
            ReportedEntityId = command.Request.ReportedEntityId,
            ReportedEntityType = entityType,
            Type = (int)command.Request.Type,
            Reason = command.Request.Reason.Trim(),
            Status = (int)ReportStatus.Pending,
            CreatedAt = _dateTimeService.UtcNow
        };

        _context.Set<Report>().Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        return report.ReportsId;
    }

    private async Task EnsureTargetCanBeReportedAsync(
        string entityType,
        Guid entityId,
        Guid reporterId,
        CancellationToken cancellationToken)
    {
        switch (entityType)
        {
            case ReportedEntityTypes.User:
                if (entityId == reporterId)
                {
                    throw new BadRequestException("You cannot report your own account.");
                }

                var userExists = await _context.Set<User>()
                    .AnyAsync(user => user.UserId == entityId, cancellationToken);
                if (!userExists)
                {
                    throw new NotFoundException("Reported user does not exist.");
                }
                break;

            case ReportedEntityTypes.JobPost:
                var jobPostExists = await _context.Set<JobPost>()
                    .AnyAsync(jobPost => jobPost.JobPostsId == entityId, cancellationToken);
                if (!jobPostExists)
                {
                    throw new NotFoundException("Reported job post does not exist.");
                }
                break;

            case ReportedEntityTypes.Review:
                var reviewExists = await _context.Set<Review>()
                    .AnyAsync(review => review.ReviewsId == entityId, cancellationToken);
                if (!reviewExists)
                {
                    throw new NotFoundException("Reported review does not exist.");
                }
                break;
        }
    }
}
