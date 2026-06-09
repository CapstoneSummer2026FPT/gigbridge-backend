using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reports.Common;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.GetReportDetail.Queries;

public class GetAdminReportDetailQueryHandler : IRequestHandler<GetAdminReportDetailQuery, ReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminReportDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportDto> Handle(GetAdminReportDetailQuery request, CancellationToken cancellationToken)
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

        return await ReportProjection.ToDtoAsync(_context, report, cancellationToken);
    }
}
