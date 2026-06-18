using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.UpdateReportStatus.Commands;

public class UpdateReportStatusCommandHandler : IRequestHandler<UpdateReportStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateReportStatusCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task Handle(UpdateReportStatusCommand command, CancellationToken cancellationToken)
    {
        var report = await _context.Set<Report>()
            .FirstOrDefaultAsync(item => item.ReportsId == command.ReportId, cancellationToken);

        if (report is null)
        {
            throw new NotFoundException("Report does not exist.");
        }

        if (command.Request.Status == ReportStatus.Reviewing &&
            report.Status != (int)ReportStatus.Pending)
        {
            throw new BadRequestException("Only pending reports can be moved to reviewing.");
        }

        if (command.Request.Status == ReportStatus.Dismissed &&
            report.Status is (int)ReportStatus.Resolved or (int)ReportStatus.Dismissed)
        {
            throw new BadRequestException("Resolved or dismissed reports cannot be dismissed again.");
        }

        if (command.Request.Status == ReportStatus.Dismissed)
        {
            var adminExists = await _context.Set<User>()
                .AnyAsync(user => user.UserId == command.AdminId && user.Role == (int)UserRole.Admin, cancellationToken);

            if (!adminExists)
            {
                throw new NotFoundException("Admin user does not exist.");
            }
        }

        var now = _dateTimeService.UtcNow;
        report.Status = (int)command.Request.Status;
        report.AdminNote = string.IsNullOrWhiteSpace(command.Request.AdminNote)
            ? report.AdminNote
            : command.Request.AdminNote.Trim();
        report.UpdatedAt = now;

        if (command.Request.Status == ReportStatus.Dismissed)
        {
            report.ResolvedByAdminId = command.AdminId;
            report.ResolvedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
