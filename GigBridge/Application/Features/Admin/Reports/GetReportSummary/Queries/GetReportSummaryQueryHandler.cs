using Application.Common.Interfaces;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.GetReportSummary.Queries;

public class GetReportSummaryQueryHandler : IRequestHandler<GetReportSummaryQuery, ReportSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetReportSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportSummaryDto> Handle(GetReportSummaryQuery request, CancellationToken cancellationToken)
    {
        var counts = await _context.Set<Report>()
            .AsNoTracking()
            .GroupBy(report => report.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        int Count(ReportStatus status) => counts.TryGetValue((int)status, out var count) ? count : 0;

        var pending = Count(ReportStatus.Pending);
        var reviewing = Count(ReportStatus.Reviewing);
        var resolved = Count(ReportStatus.Resolved);
        var dismissed = Count(ReportStatus.Dismissed);

        return new ReportSummaryDto
        {
            Total = counts.Values.Sum(),
            Pending = pending,
            Reviewing = reviewing,
            Resolved = resolved,
            Dismissed = dismissed,
            Open = pending + reviewing
        };
    }
}
