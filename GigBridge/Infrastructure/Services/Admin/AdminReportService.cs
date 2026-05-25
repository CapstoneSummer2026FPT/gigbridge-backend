using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs.Admin;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminReportService : AdminServiceBase, IAdminReportService
{
    private readonly IMapper _mapper;

    public AdminReportService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ReportDto>> GetAllAsync(ReportPageQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var reports = DbContext.Set<Report>().AsNoTracking();
        if (query.Status.HasValue)
        {
            reports = reports.Where(x => x.Status == query.Status);
        }

        if (query.Type.HasValue)
        {
            reports = reports.Where(x => x.Type == query.Type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            reports = reports.Where(x => x.Reason.ToLower().Contains(search) || x.ReportedEntityType.ToLower().Contains(search));
        }

        reports = FilterDates(reports, query, x => x.CreatedAt);
        return await ToPageAsync(reports.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<ReportDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<ReportDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<Report>().AsNoTracking().Where(x => x.ReportsId == id)
            .ProjectTo<ReportDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(Report), id);
    }

    public async Task<ReportDto> ReviewAsync(Guid id, ReportReviewRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var report = await DbContext.Set<Report>().SingleOrDefaultAsync(x => x.ReportsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Report), id);
        if (report.Status != 0)
        {
            throw Invalid("status", "Only a pending report can be moved to reviewing.");
        }

        var oldValues = new { report.Status, report.AdminNote, report.UpdatedAt };
        report.Status = 1;
        report.AdminNote = request.AdminNote;
        report.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "ReportReviewStarted", id, "Report", oldValues,
            new { report.Status, report.AdminNote, report.UpdatedAt });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ReportDto> ResolveAsync(Guid id, ReportResolutionRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        if (request.Status is not (2 or 3))
        {
            throw Invalid("status", "Resolution status must be 2 (resolved) or 3 (dismissed).");
        }

        var report = await DbContext.Set<Report>().SingleOrDefaultAsync(x => x.ReportsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Report), id);
        var oldValues = new
        {
            report.Status,
            report.AdminNote,
            report.ResolvedByAdminId,
            report.ResolvedAt,
            report.AdminAttachmentUrl,
            report.AdminAttachmentFileName
        };

        report.Status = request.Status;
        report.AdminNote = request.AdminNote;
        report.AdminAttachmentUrl = request.AdminAttachmentUrl;
        report.AdminAttachmentFileName = request.AdminAttachmentFileName;
        report.ResolvedByAdminId = actor.AdminId;
        report.ResolvedAt = DateTime.UtcNow;
        report.UpdatedAt = report.ResolvedAt;
        AddAudit(actor, request.Status == 2 ? "ReportResolved" : "ReportDismissed", id, "Report", oldValues,
            new
            {
                report.Status,
                report.AdminNote,
                report.ResolvedByAdminId,
                report.ResolvedAt,
                report.AdminAttachmentUrl,
                report.AdminAttachmentFileName
            });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

}
