using Application.Common.Interfaces;
using Application.Features.Reports.Common;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.GetReports.Queries;

public class GetReportsQueryHandler : IRequestHandler<GetReportsQuery, ReportsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetReportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportsResponse> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _context.Set<Report>()
            .AsNoTracking()
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedByAdmin)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(report => report.Status == (int)request.Status.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(report => report.Type == (int)request.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ReportedEntityType) &&
            ReportedEntityTypes.IsSupported(request.ReportedEntityType))
        {
            var entityType = ReportedEntityTypes.Normalize(request.ReportedEntityType);
            query = query.Where(report => report.ReportedEntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = $"%{request.Search.Trim()}%";

            query = query.Where(report =>
                EF.Functions.Like(report.Reason, keyword) ||
                (report.AdminNote != null && EF.Functions.Like(report.AdminNote, keyword)) ||
                EF.Functions.Like(report.Reporter.FullName, keyword) ||
                EF.Functions.Like(report.Reporter.Email, keyword));
        }

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
