using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reports.Common;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Public.GetReportDetail.Queries;

public class GetReportDetailQueryHandler : IRequestHandler<GetReportDetailQuery, ReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetReportDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportDto> Handle(GetReportDetailQuery request, CancellationToken cancellationToken)
    {
        var report = await _context.Set<Report>()
            .AsNoTracking()
            .Include(item => item.Reporter)
            .Include(item => item.ResolvedByAdmin)
            .FirstOrDefaultAsync(item => item.ReportsId == request.ReportId, cancellationToken);

        if (report is null)
        {
            throw new NotFoundException("Report does not exist.");
        }

        if (report.ReporterId != request.UserId)
        {
            throw new ForbiddenAccessException("You can only view your own reports.");
        }

        return await ReportProjection.ToDtoAsync(_context, report, cancellationToken);
    }
}
