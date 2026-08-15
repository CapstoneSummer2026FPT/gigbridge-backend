using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.AuditLogs.Services;
using Domain.Entities;
using Domain.Enums.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Evidence;

public sealed record ReportEvidenceFile(Stream Content, string FileName, string ContentType, long Length, string? Description);
public sealed record AddReportEvidenceCommand(Guid ReportId, Guid UserId, IReadOnlyList<ReportEvidenceFile> Files) : IRequest<IReadOnlyList<Admin.Reports.AccountReports.AccountReportEvidenceDto>>;
public sealed record GetReportEvidenceDownloadQuery(Guid ReportId, Guid EvidenceId, Guid UserId, bool IsAdmin) : IRequest<ReportEvidenceDownloadDto>;
public sealed record ReportEvidenceDownloadDto(Guid EvidenceId, string FileName, string DownloadUrl);

public sealed class ReportEvidenceHandler : IRequestHandler<AddReportEvidenceCommand, IReadOnlyList<Admin.Reports.AccountReports.AccountReportEvidenceDto>>, IRequestHandler<GetReportEvidenceDownloadQuery, ReportEvidenceDownloadDto>
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase) {
        [".pdf"]=["application/pdf"], [".png"]=["image/png"], [".jpg"]=["image/jpeg"], [".jpeg"]=["image/jpeg"], [".webp"]=["image/webp"], [".txt"]=["text/plain"], [".doc"]=["application/msword"], [".docx"]=["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] };
    private readonly IApplicationDbContext _context; private readonly IMediaService _media; private readonly IAdminAuditService _audit;
    public ReportEvidenceHandler(IApplicationDbContext context, IMediaService media, IAdminAuditService audit) { _context = context; _media = media; _audit = audit; }
    public async Task<IReadOnlyList<Admin.Reports.AccountReports.AccountReportEvidenceDto>> Handle(AddReportEvidenceCommand q, CancellationToken ct)
    {
        if (q.Files.Count is < 1 or > 5) throw new BadRequestException("Upload between one and five evidence files.");
        var report = await _context.Set<Report>().Include(x => x.ReportEvidences).FirstOrDefaultAsync(x => x.ReportsId == q.ReportId, ct) ?? throw new NotFoundException("Report does not exist.");
        if (report.ReporterId != q.UserId) throw new ForbiddenAccessException("Only the reporter can upload evidence.");
        if (report.Status is (int)ReportStatus.Resolved or (int)ReportStatus.Dismissed) throw new ConflictException("Evidence cannot be added to a finalized report.");
        if (report.ReportEvidences.Count + q.Files.Count > 5) throw new BadRequestException("A report can contain at most five evidence files.");
        var uploaded = new List<(string Key, string Type)>(); var rows = new List<ReportEvidence>();
        try {
            foreach (var file in q.Files) { Validate(file); var name = Path.GetFileName(file.FileName.Trim()); var key = await _media.UploadPrivateFileAsync(file.Content, name, file.ContentType, "report-evidence", ct); uploaded.Add((key, file.ContentType)); rows.Add(new ReportEvidence { ReportEvidenceId = Guid.NewGuid(), ReportId = report.ReportsId, UploadedByUserId = q.UserId, StorageKey = key, OriginalFileName = name, ContentType = file.ContentType, FileSize = file.Length, Description = file.Description?.Trim(), CreatedAt = DateTime.UtcNow }); }
            _context.Set<ReportEvidence>().AddRange(rows); await _context.SaveChangesAsync(ct);
        } catch { foreach (var item in uploaded) { try { await _media.DeletePrivateFileAsync(item.Key, item.Type, ct); } catch { } } throw; }
        return rows.Select(x => new Admin.Reports.AccountReports.AccountReportEvidenceDto(x.ReportEvidenceId, x.OriginalFileName, x.ContentType, x.FileSize, x.Description, x.UploadedByUserId, x.CreatedAt)).ToList();
    }
    public async Task<ReportEvidenceDownloadDto> Handle(GetReportEvidenceDownloadQuery q, CancellationToken ct)
    {
        var row = await _context.Set<ReportEvidence>().AsNoTracking().Include(x => x.Report).FirstOrDefaultAsync(x => x.ReportId == q.ReportId && x.ReportEvidenceId == q.EvidenceId, ct) ?? throw new NotFoundException("Report evidence does not exist.");
        if (!q.IsAdmin && row.Report.ReporterId != q.UserId) throw new ForbiddenAccessException("You cannot access this evidence.");
        if (q.IsAdmin) { _audit.Add(q.UserId, AdminAuditActions.AccountReportEvidenceDownloaded, "Report", q.ReportId, null, new { evidenceId = row.ReportEvidenceId, row.OriginalFileName }); await _context.SaveChangesAsync(ct); }
        return new(row.ReportEvidenceId, row.OriginalFileName, await _media.GetPrivateDownloadUrlAsync(row.StorageKey, row.ContentType, ct));
    }
    private static void Validate(ReportEvidenceFile f) { if (f.Length <= 0 || f.Length > 100L * 1024 * 1024) throw new BadRequestException("Evidence file must be between 1 byte and 100 MB."); var supplied = f.FileName.Trim(); var name = Path.GetFileName(supplied); if (string.IsNullOrWhiteSpace(name) || name.Length > 500 || !string.Equals(name, supplied, StringComparison.Ordinal) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new BadRequestException("Evidence filename is invalid."); var ext = Path.GetExtension(name); if (!Allowed.TryGetValue(ext, out var types) || !types.Contains(f.ContentType, StringComparer.OrdinalIgnoreCase)) throw new BadRequestException("Unsupported evidence file type."); }
}
