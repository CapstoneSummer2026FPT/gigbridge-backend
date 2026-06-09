using Application.Common.Interfaces;
using Application.Features.Reports.Common;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Public.GetMyReports.Queries;

public class GetMyReportsQueryHandler : IRequestHandler<GetMyReportsQuery, ReportsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetMyReportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportsResponse> Handle(GetMyReportsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _context.Set<Report>()
            .AsNoTracking()
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedByAdmin)
            .Where(report => report.ReporterId == request.UserId);

        var total = await query.CountAsync(cancellationToken);
        var reports = await query
            .OrderByDescending(report => report.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ReportsResponse
        {
            Items = await ReportProjection.ToDtosAsync(_context, reports, cancellationToken),
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }
}
