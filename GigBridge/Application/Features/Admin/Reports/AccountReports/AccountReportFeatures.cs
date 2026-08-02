using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reports.AccountReports;

public sealed record AccountReportEvidenceDto(Guid Id, string FileName, string ContentType, long FileSize, string? Description, Guid UploadedByUserId, DateTime CreatedAt);
public sealed record AccountReportListItemDto(Guid Id, Guid ReporterId, string ReporterName, int ReporterRole, Guid ReportedUserId, string ReportedUserName, int ReportedUserRole,
    int Type, int Status, string Reason, DateTime CreatedAt, int EvidenceCount, int AccountStatus, int ViolationCount,
    bool IsFlagged, DateTime? SuspendedUntil, Guid? AssignedAdminId, string? AssignedAdminName);
public sealed record AccountReportDetailDto(AccountReportListItemDto Report, string? Description, string? AdminNote, int? ResolutionAction,
    DateTime? ResolvedAt, IReadOnlyList<AccountReportEvidenceDto> Evidence, IReadOnlyList<Admin.Users.Detail.AdminUserReportDto> PreviousReports,
    IReadOnlyList<Admin.Users.Detail.AdminViolationDto> Violations, IReadOnlyList<Admin.Users.Detail.AdminUserAuditDto> AuditLogs);
public sealed record GetAccountReportsQuery(int Page = 1, int PageSize = 20, ReportStatus? Status = null, ReportType? Type = null,
    Guid? ReporterId = null, Guid? ReportedUserId = null, DateTime? From = null, DateTime? To = null,
    bool? HasEvidence = null, AccountStatus? AccountStatus = null, bool? IsFlagged = null, string? Search = null) : IRequest<PaginatedList<AccountReportListItemDto>>;
public sealed record GetAccountReportDetailQuery(Guid ReportId) : IRequest<AccountReportDetailDto>;

public sealed class AccountReportQueryHandler : IRequestHandler<GetAccountReportsQuery, PaginatedList<AccountReportListItemDto>>, IRequestHandler<GetAccountReportDetailQuery, AccountReportDetailDto>
{
    private readonly IApplicationDbContext _context; public AccountReportQueryHandler(IApplicationDbContext context) => _context = context;
    private sealed class AccountReportRow { public required Report Report { get; init; } public required User Target { get; init; } }
    private IQueryable<AccountReportRow> Base() =>
        from r in _context.Set<Report>().AsNoTracking().Include(x => x.Reporter).Include(x => x.AssignedAdmin).Include(x => x.ReportEvidences)
        join u in _context.Set<User>().AsNoTracking() on r.ReportedEntityId equals u.UserId
        where r.ReportedEntityType == ReportedEntityTypes.User select new AccountReportRow { Report = r, Target = u };

    public async Task<PaginatedList<AccountReportListItemDto>> Handle(GetAccountReportsQuery q, CancellationToken ct)
    {
        var page = Math.Max(q.Page, 1); var size = Math.Clamp(q.PageSize, 1, 100); var query = Base();
        if (q.Status.HasValue) query = query.Where(x => x.Report.Status == (int)q.Status);
        if (q.Type.HasValue) query = query.Where(x => x.Report.Type == (int)q.Type);
        if (q.ReporterId.HasValue) query = query.Where(x => x.Report.ReporterId == q.ReporterId);
        if (q.ReportedUserId.HasValue) query = query.Where(x => x.Target.UserId == q.ReportedUserId);
        if (q.From.HasValue) query = query.Where(x => x.Report.CreatedAt >= q.From);
        if (q.To.HasValue) query = query.Where(x => x.Report.CreatedAt <= q.To);
        if (q.HasEvidence.HasValue) query = q.HasEvidence.Value ? query.Where(x => x.Report.ReportEvidences.Any()) : query.Where(x => !x.Report.ReportEvidences.Any());
        if (q.AccountStatus.HasValue) query = query.Where(x => x.Target.AccountStatus == (int)q.AccountStatus);
        if (q.IsFlagged.HasValue) query = query.Where(x => x.Target.IsFlagged == q.IsFlagged);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var term = q.Search.Trim().ToLower(); query = query.Where(x => x.Report.Reason.ToLower().Contains(term) || x.Report.Reporter.FullName.ToLower().Contains(term) || x.Target.FullName.ToLower().Contains(term) || x.Target.Email.ToLower().Contains(term)); }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Report.Status).ThenByDescending(x => x.Report.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new AccountReportListItemDto(x.Report.ReportsId, x.Report.ReporterId,
                x.Report.Reporter.FullName, x.Report.Reporter.Role, x.Target.UserId, x.Target.FullName, x.Target.Role, x.Report.Type,
                x.Report.Status, x.Report.Reason, x.Report.CreatedAt, x.Report.ReportEvidences.Count,
                x.Target.AccountStatus, x.Target.ViolationCount, x.Target.IsFlagged, x.Target.SuspendedUntil,
                x.Report.AssignedAdminId, x.Report.AssignedAdmin != null ? x.Report.AssignedAdmin.FullName : null))
            .ToListAsync(ct);
        return new(rows, count, page, size);
    }
    public async Task<AccountReportDetailDto> Handle(GetAccountReportDetailQuery q, CancellationToken ct)
    {
        var report = await _context.Set<Report>().AsNoTracking().Include(x => x.Reporter).Include(x => x.AssignedAdmin).Include(x => x.ReportEvidences)
            .FirstOrDefaultAsync(x => x.ReportsId == q.ReportId && x.ReportedEntityType == ReportedEntityTypes.User, ct)
            ?? throw new NotFoundException("Account report does not exist.");
        var target = await _context.Set<User>().AsNoTracking().FirstAsync(x => x.UserId == report.ReportedEntityId, ct);
        var previous = await _context.Set<Report>().AsNoTracking().Include(x => x.ReportEvidences).Where(x => x.ReportedEntityType == ReportedEntityTypes.User && x.ReportedEntityId == target.UserId && x.ReportsId != q.ReportId).OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
        var violations = await _context.Set<UserViolation>().AsNoTracking().Where(x => x.UserId == target.UserId).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
        var audits = await _context.Set<AdminAuditLog>().AsNoTracking().Where(x => x.EntityType == "Report" && x.EntityId == q.ReportId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return new(Map(report, target), report.Description, report.AdminNote, report.ResolutionAction, report.ResolvedAt,
            report.ReportEvidences.OrderByDescending(x => x.CreatedAt).Select(x => new AccountReportEvidenceDto(x.ReportEvidenceId, x.OriginalFileName, x.ContentType, x.FileSize, x.Description, x.UploadedByUserId, x.CreatedAt)).ToList(),
            previous.Select(x => new Admin.Users.Detail.AdminUserReportDto(x.ReportsId, x.Type, x.Status, x.Reason, x.Description, x.ReportEvidences.Count, x.CreatedAt)).ToList(),
            violations.Select(x => new Admin.Users.Detail.AdminViolationDto(x.UserViolationId, x.SourceType, x.DisputeId, x.ReportId, x.ManualActionId, x.ViolationNumber, x.ViolationType, x.Reason, x.Description, x.ActionTaken, x.SuspendedUntil, x.IsActive, x.CreatedAt)).ToList(),
            audits.Select(x => new Admin.Users.Detail.AdminUserAuditDto(x.AdminAuditLogsId, x.Action, x.EntityType, x.EntityId, x.OldValues, x.NewValues, x.CreatedAt)).ToList());
    }
    private static AccountReportListItemDto Map(Report r, User u) => new(r.ReportsId, r.ReporterId, r.Reporter.FullName, r.Reporter.Role, u.UserId, u.FullName, u.Role, r.Type, r.Status, r.Reason, r.CreatedAt, r.ReportEvidences.Count, u.AccountStatus, u.ViolationCount, u.IsFlagged, u.SuspendedUntil, r.AssignedAdminId, r.AssignedAdmin?.FullName);
}

public sealed record AccountReportStatusRequest(ReportStatus Status, string? AdminNote);
public sealed record ResolveAccountReportRequest(AccountReportResolutionAction Action, UserViolationType? ViolationType, string Reason, string? Description, string? AdminNote, DateTime? SuspendedUntil);
public sealed record UpdateAccountReportStatusCommand(Guid AdminId, Guid ReportId, AccountReportStatusRequest Request) : IRequest<AccountReportDetailDto>;
public sealed record ResolveAccountReportCommand(Guid AdminId, Guid ReportId, ResolveAccountReportRequest Request) : IRequest<AccountReportDetailDto>;

public sealed class AccountReportMutationHandler : IRequestHandler<UpdateAccountReportStatusCommand, AccountReportDetailDto>, IRequestHandler<ResolveAccountReportCommand, AccountReportDetailDto>
{
    private readonly IApplicationDbContext _context; private readonly IUserAccountStatusService _status; private readonly IAdminAuditService _audit; private readonly IMediator _mediator;
    public AccountReportMutationHandler(IApplicationDbContext context, IUserAccountStatusService status, IAdminAuditService audit, IMediator mediator) { _context = context; _status = status; _audit = audit; _mediator = mediator; }
    public async Task<AccountReportDetailDto> Handle(UpdateAccountReportStatusCommand q, CancellationToken ct)
    {
        if (q.Request.Status is not ReportStatus.Reviewing and not ReportStatus.Dismissed) throw new BadRequestException("Account Report status can only move to Reviewing or Dismissed through this action.");
        await using var tx = await _context.BeginTransactionAsync(ct); await tx.AcquireTransactionLockAsync(AccountEnforcementLock.ForReport(q.ReportId), ct);
        var report = await Load(q.AdminId, q.ReportId, ct); EnsureOpen(report);
        var old = new { report.Status, report.AssignedAdminId, report.AdminNote };
        report.Status = (int)q.Request.Status; report.AssignedAdminId ??= q.AdminId; report.AssignedAt ??= DateTime.UtcNow; report.AdminNote = q.Request.AdminNote?.Trim(); report.UpdatedAt = DateTime.UtcNow;
        if (q.Request.Status == ReportStatus.Dismissed) { report.ResolvedByAdminId = q.AdminId; report.ResolvedAt = DateTime.UtcNow; report.ResolutionAction = (int)AccountReportResolutionAction.None; }
        _audit.Add(q.AdminId, q.Request.Status == ReportStatus.Reviewing ? AdminAuditActions.AccountReportReviewing : AdminAuditActions.AccountReportDismissed, "Report", report.ReportsId, old, new { report.Status, report.AssignedAdminId, report.AdminNote });
        await SaveResolutionAsync(ct); await tx.CommitAsync(ct); return await _mediator.Send(new GetAccountReportDetailQuery(report.ReportsId), ct);
    }
    public async Task<AccountReportDetailDto> Handle(ResolveAccountReportCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Request.Reason)) throw new BadRequestException("Resolution reason is required.");
        if (q.Request.Action != AccountReportResolutionAction.None && !q.Request.ViolationType.HasValue) throw new BadRequestException("Violation type is required for account enforcement.");
        if (q.Request.Action == AccountReportResolutionAction.Suspension && (!q.Request.SuspendedUntil.HasValue || q.Request.SuspendedUntil <= DateTime.UtcNow)) throw new BadRequestException("Suspension end time must be in the future.");
        await using var tx = await _context.BeginTransactionAsync(ct); await tx.AcquireTransactionLockAsync(AccountEnforcementLock.ForReport(q.ReportId), ct);
        var report = await Load(q.AdminId, q.ReportId, ct); EnsureOpen(report); await tx.AcquireTransactionLockAsync(AccountEnforcementLock.ForUser(report.ReportedEntityId), ct);
        var user = await _context.Set<User>().FirstAsync(x => x.UserId == report.ReportedEntityId, ct); if (user.Role == (int)UserRole.Admin) throw new ConflictException("Admin accounts are protected from account-report enforcement.");
        var old = new { report.Status, report.AdminNote, user.ViolationCount, user.AccountStatus, user.IsActive, user.IsFlagged };
        AccountEnforcementResult? enforcement = null;
        if (q.Request.Action != AccountReportResolutionAction.None)
            enforcement = await _status.ApplyViolationAsync(user, new(UserViolationSourceType.Report, ReportId: report.ReportsId), q.Request.ViolationType!.Value,
                q.Request.Reason, q.Request.Description, q.AdminId, q.Request.Action switch { AccountReportResolutionAction.Warning => AccountEnforcementAction.Warning, AccountReportResolutionAction.Suspension => AccountEnforcementAction.Suspension, _ => AccountEnforcementAction.PermanentBan }, q.Request.SuspendedUntil, ct);
        report.Status = (int)ReportStatus.Resolved; report.ResolutionAction = (int)q.Request.Action; report.AdminNote = q.Request.AdminNote?.Trim(); report.ResolvedByAdminId = q.AdminId; report.ResolvedAt = DateTime.UtcNow; report.AssignedAdminId ??= q.AdminId; report.AssignedAt ??= DateTime.UtcNow; report.UpdatedAt = DateTime.UtcNow;
        var action = q.Request.Action switch { AccountReportResolutionAction.Warning => AdminAuditActions.AccountReportWarning, AccountReportResolutionAction.Suspension => AdminAuditActions.AccountReportSuspension, AccountReportResolutionAction.PermanentBan => AdminAuditActions.AccountReportBan, _ => AdminAuditActions.AccountReportResolved };
        _audit.Add(q.AdminId, action, "Report", report.ReportsId, old, new { report.Status, report.ResolutionAction, report.AdminNote, affectedUserId = user.UserId, user.ViolationCount, user.AccountStatus, user.IsActive, user.IsFlagged, user.SuspendedUntil, user.BannedAt, enforcement });
        await SaveResolutionAsync(ct); await tx.CommitAsync(ct); return await _mediator.Send(new GetAccountReportDetailQuery(report.ReportsId), ct);
    }
    private async Task<Report> Load(Guid adminId, Guid id, CancellationToken ct) { if (!await _context.Set<User>().AnyAsync(x => x.UserId == adminId && x.Role == (int)UserRole.Admin, ct)) throw new ForbiddenAccessException("Admin access is required."); return await _context.Set<Report>().FirstOrDefaultAsync(x => x.ReportsId == id && x.ReportedEntityType == ReportedEntityTypes.User, ct) ?? throw new NotFoundException("Account report does not exist."); }
    private static void EnsureOpen(Report r) { if (r.Status is (int)ReportStatus.Resolved or (int)ReportStatus.Dismissed) throw new ConflictException("This report has already been finalized."); }
    private async Task SaveResolutionAsync(CancellationToken ct)
    {
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("The report changed while it was being processed. Refresh and retry."); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UserViolations_UserId_", StringComparison.OrdinalIgnoreCase) == true)
        { throw new ConflictException("This account-report enforcement has already been processed."); }
    }
}
