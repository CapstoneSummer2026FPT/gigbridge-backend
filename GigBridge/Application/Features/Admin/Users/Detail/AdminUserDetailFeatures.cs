using Application.Common.InternalServices.Accounts.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.AuditLogs.Services;
using Application.Common.Models;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.Detail;

public sealed record AdminUserProfileDto(string Kind, string? Title, string? Bio, string? CompanyName,
    string? Industry, string? Location, IReadOnlyList<string> Skills, IReadOnlyList<string> Categories,
    IReadOnlyList<string> PortfolioUrls, IReadOnlyList<string> WorkExperience);
public sealed record AdminWalletSummaryDto(decimal AvailableTokens, decimal WithdrawableTokens, decimal HeldTokens, decimal PendingWithdrawalTokens);
public sealed record AdminSubscriptionSummaryDto(string PlanName, int Status, DateTime StartDate, DateTime EndDate);
public sealed record AdminViolationDto(Guid Id, int SourceType, Guid? DisputeId, Guid? ReportId, Guid? ManualActionId,
    int Number, int Type, string Reason, string? Description, int ActionTaken, DateTime? SuspendedUntil, bool IsActive, DateTime CreatedAt);
public sealed record AdminUserReportDto(Guid Id, int Type, int Status, string Reason, string? Description, int EvidenceCount, DateTime CreatedAt);
public sealed record AdminUserAuditDto(Guid Id, string Action, string? EntityType, Guid? EntityId, string? OldValues, string? NewValues, DateTime CreatedAt);
public sealed record AdminUserDetailDto(Guid UserId, string FullName, string Email, string? Avatar, int? EloPoints, int Role, DateTime CreatedAt,
    bool IsEmailVerified, bool IsActive, int AccountStatus, bool IsFlagged, int ViolationCount,
    DateTime? SuspendedUntil, DateTime? BannedAt, string? BanReason, AdminSubscriptionSummaryDto? Subscription,
    AdminUserProfileDto? Profile, AdminWalletSummaryDto? Wallet, IReadOnlyList<AdminUserReportDto> RecentReports,
    IReadOnlyList<AdminViolationDto> RecentViolations, IReadOnlyList<AdminUserAuditDto> RecentAuditLogs);

public sealed record GetAdminUserDetailQuery(Guid AdminId, Guid UserId) : IRequest<AdminUserDetailDto>;
public sealed record GetAdminUserViolationsQuery(Guid AdminId, Guid UserId, int Page = 1, int PageSize = 20) : IRequest<PaginatedList<AdminViolationDto>>;
public sealed record GetAdminUserReportsQuery(Guid AdminId, Guid UserId, int Page = 1, int PageSize = 20) : IRequest<PaginatedList<AdminUserReportDto>>;
public sealed record GetAdminUserAuditLogsQuery(Guid AdminId, Guid UserId, int Page = 1, int PageSize = 20) : IRequest<PaginatedList<AdminUserAuditDto>>;

public sealed class AdminUserDetailQueryHandler :
    IRequestHandler<GetAdminUserDetailQuery, AdminUserDetailDto>,
    IRequestHandler<GetAdminUserViolationsQuery, PaginatedList<AdminViolationDto>>,
    IRequestHandler<GetAdminUserReportsQuery, PaginatedList<AdminUserReportDto>>,
    IRequestHandler<GetAdminUserAuditLogsQuery, PaginatedList<AdminUserAuditDto>>
{
    private readonly IApplicationDbContext _context;
    public AdminUserDetailQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminUserDetailDto> Handle(GetAdminUserDetailQuery q, CancellationToken ct)
    {
        await EnsureAdmin(q.AdminId, ct);
        var user = await _context.Set<User>().AsNoTracking()
            .Include(x => x.ClientProfile)
            .Include(x => x.FreelancerProfile).ThenInclude(x => x!.FreelancerSkills).ThenInclude(x => x.Skills)
            .Include(x => x.FreelancerProfile).ThenInclude(x => x!.FreelancerProfileCategories).ThenInclude(x => x.MajorCategory).ThenInclude(x => x.Category)
            .Include(x => x.FreelancerProfile).ThenInclude(x => x!.PortfolioItems)
            .Include(x => x.FreelancerProfile).ThenInclude(x => x!.WorkExperiences)
            .Include(x => x.UserEloScore).Include(x => x.UserWallet)
            .FirstOrDefaultAsync(x => x.UserId == q.UserId, ct) ?? throw new NotFoundException("User does not exist.");
        var reports = await Reports(q.UserId).Take(5).ToListAsync(ct);
        var violations = await Violations(q.UserId).Take(5).ToListAsync(ct);
        var reportIds = await _context.Set<Report>().Where(x => x.ReportedEntityType == ReportedEntityTypes.User && x.ReportedEntityId == q.UserId).Select(x => x.ReportsId).ToListAsync(ct);
        var audits = await Audits(q.UserId, reportIds).Take(10).ToListAsync(ct);
        var subscription = await _context.Set<Subscription>().AsNoTracking().Include(x => x.SubscriptionPlans)
            .Where(x => x.UserId == q.UserId).OrderByDescending(x => x.EndDate).FirstOrDefaultAsync(ct);
        return new AdminUserDetailDto(user.UserId, user.FullName, user.Email, user.Avatar, user.UserEloScore?.CurrentPoints, user.Role, user.CreatedAt,
            user.IsEmailVerified, user.IsActive, user.AccountStatus, user.IsFlagged, user.ViolationCount,
            user.SuspendedUntil, user.BannedAt, user.BanReason,
            subscription is null ? null : new(subscription.SubscriptionPlans.Name, (int)subscription.Status, subscription.StartDate, subscription.EndDate),
            MapProfile(user), user.UserWallet is null ? null : new(user.UserWallet.AvailableTokens, user.UserWallet.WithdrawableTokens, user.UserWallet.HeldTokens, user.UserWallet.PendingWithdrawalTokens),
            reports.Select(MapReport).ToList(), violations.Select(MapViolation).ToList(), audits.Select(MapAudit).ToList());
    }

    public async Task<PaginatedList<AdminViolationDto>> Handle(GetAdminUserViolationsQuery q, CancellationToken ct)
    {
        await EnsureAdmin(q.AdminId, ct);
        return await Page(Violations(q.UserId).Select(x => new AdminViolationDto(x.UserViolationId, x.SourceType, x.DisputeId, x.ReportId, x.ManualActionId, x.ViolationNumber, x.ViolationType, x.Reason, x.Description, x.ActionTaken, x.SuspendedUntil, x.IsActive, x.CreatedAt)), q.Page, q.PageSize, ct);
    }

    public async Task<PaginatedList<AdminUserReportDto>> Handle(GetAdminUserReportsQuery q, CancellationToken ct)
    {
        await EnsureAdmin(q.AdminId, ct);
        return await Page(Reports(q.UserId).Select(x => new AdminUserReportDto(x.ReportsId, x.Type, x.Status, x.Reason, x.Description, x.ReportEvidences.Count, x.CreatedAt)), q.Page, q.PageSize, ct);
    }

    public async Task<PaginatedList<AdminUserAuditDto>> Handle(GetAdminUserAuditLogsQuery q, CancellationToken ct)
    {
        await EnsureAdmin(q.AdminId, ct);
        var ids = await _context.Set<Report>().Where(x => x.ReportedEntityType == ReportedEntityTypes.User && x.ReportedEntityId == q.UserId).Select(x => x.ReportsId).ToListAsync(ct);
        return await Page(Audits(q.UserId, ids).Select(x => new AdminUserAuditDto(x.AdminAuditLogsId, x.Action, x.EntityType, x.EntityId, x.OldValues, x.NewValues, x.CreatedAt)), q.Page, q.PageSize, ct);
    }

    private async Task EnsureAdmin(Guid id, CancellationToken ct)
    {
        if (!await _context.Set<User>().AnyAsync(x => x.UserId == id && x.Role == (int)UserRole.Admin, ct)) throw new ForbiddenAccessException("Admin access is required.");
    }

    private IQueryable<UserViolation> Violations(Guid id) => _context.Set<UserViolation>().AsNoTracking().Where(x => x.UserId == id).OrderByDescending(x => x.CreatedAt);
    private IQueryable<Report> Reports(Guid id) => _context.Set<Report>().AsNoTracking().Include(x => x.ReportEvidences).Where(x => x.ReportedEntityType == ReportedEntityTypes.User && x.ReportedEntityId == id).OrderByDescending(x => x.CreatedAt);
    private IQueryable<AdminAuditLog> Audits(Guid id, IReadOnlyCollection<Guid> reportIds) => _context.Set<AdminAuditLog>().AsNoTracking().Where(x => (x.EntityType == "User" && x.EntityId == id) || (x.EntityType == "Report" && x.EntityId.HasValue && reportIds.Contains(x.EntityId.Value))).OrderByDescending(x => x.CreatedAt);
    private static AdminViolationDto MapViolation(UserViolation x) => new(x.UserViolationId, x.SourceType, x.DisputeId, x.ReportId, x.ManualActionId, x.ViolationNumber, x.ViolationType, x.Reason, x.Description, x.ActionTaken, x.SuspendedUntil, x.IsActive, x.CreatedAt);
    private static AdminUserReportDto MapReport(Report x) => new(x.ReportsId, x.Type, x.Status, x.Reason, x.Description, x.ReportEvidences.Count, x.CreatedAt);
    private static AdminUserAuditDto MapAudit(AdminAuditLog x) => new(x.AdminAuditLogsId, x.Action, x.EntityType, x.EntityId, x.OldValues, x.NewValues, x.CreatedAt);
    private static AdminUserProfileDto? MapProfile(User u) => u.ClientProfile is not null
        ? new("Client", null, null, u.ClientProfile.CompanyName, u.ClientProfile.Industry, u.ClientProfile.Location, [], [], [], [])
        : u.FreelancerProfile is null ? null : new("Freelancer", u.FreelancerProfile.Title, u.FreelancerProfile.Bio, null, null, u.FreelancerProfile.Location,
            u.FreelancerProfile.FreelancerSkills.Select(x => x.Skills.Name).ToList(), u.FreelancerProfile.FreelancerProfileCategories.Select(x => x.MajorCategory.Category.Name).ToList(),
            u.FreelancerProfile.PortfolioItems.Where(x => x.ProjectUrl != null).Select(x => x.ProjectUrl!).ToList(),
            u.FreelancerProfile.WorkExperiences.Select(x => $"{x.Title} · {x.CompanyName}").ToList());

    private static async Task<PaginatedList<T>> Page<T>(IQueryable<T> query, int page, int size, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, PaginatedQuery.MaxPageSize);
        var count = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows, count, page, size);
    }
}

public sealed record AdminEnforcementRequest(Guid RequestId, UserViolationType ViolationType, string Reason, string? Description, DateTime? SuspendedUntil);
public sealed record AdminReasonRequest(string Reason);
public sealed record EnforceAdminUserCommand(Guid AdminId, Guid UserId, AccountEnforcementAction Action, AdminEnforcementRequest Request) : IRequest<AccountEnforcementResult>;
public sealed record ClearAdminUserSuspensionCommand(Guid AdminId, Guid UserId, string Reason, bool Restore) : IRequest<bool>;

public sealed class AdminUserEnforcementHandler :
    IRequestHandler<EnforceAdminUserCommand, AccountEnforcementResult>, IRequestHandler<ClearAdminUserSuspensionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAccountStatusService _status;
    private readonly IAdminAuditService _audit;

    public AdminUserEnforcementHandler(IApplicationDbContext context, IUserAccountStatusService status, IAdminAuditService audit)
    {
        _context = context;
        _status = status;
        _audit = audit;
    }

    public async Task<AccountEnforcementResult> Handle(EnforceAdminUserCommand q, CancellationToken ct)
    {
        if (q.Request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(q.Request.Reason)) throw new BadRequestException("Request ID and reason are required.");
        await using var tx = await _context.BeginTransactionAsync(ct);
        await tx.AcquireTransactionLockAsync(AccountEnforcementLock.ForUser(q.UserId), ct);
        var user = await GetTarget(q.AdminId, q.UserId, ct);
        var before = new { user.ViolationCount, user.AccountStatus, user.IsActive, user.IsFlagged };
        var result = await _status.ApplyViolationAsync(user, new(UserViolationSourceType.ManualAdmin, ManualActionId: q.Request.RequestId), q.Request.ViolationType,
            q.Request.Reason, q.Request.Description, q.AdminId, q.Action, q.Request.SuspendedUntil, ct);
        if (!result.Duplicate) _audit.Add(q.AdminId, q.Action switch { AccountEnforcementAction.Warning => AdminAuditActions.WarningIssued, AccountEnforcementAction.Suspension => AdminAuditActions.UserSuspended, _ => AdminAuditActions.UserBanned }, "User", user.UserId, before, new { user.ViolationCount, user.AccountStatus, user.IsActive, user.IsFlagged, user.SuspendedUntil, user.BannedAt, reason = q.Request.Reason, requestId = q.Request.RequestId });
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The user account changed while enforcement was being applied. Refresh and retry.");
        }
        catch (DbUpdateException ex) when (IsViolationConflict(ex))
        {
            throw new ConflictException("This enforcement request has already been processed.");
        }
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<bool> Handle(ClearAdminUserSuspensionCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Reason)) throw new BadRequestException("Reason is required.");
        await using var tx = await _context.BeginTransactionAsync(ct);
        await tx.AcquireTransactionLockAsync(AccountEnforcementLock.ForUser(q.UserId), ct);
        var user = await GetTarget(q.AdminId, q.UserId, ct);
        var before = new { user.AccountStatus, user.IsActive, user.SuspendedUntil, user.BannedAt };
        if (q.Restore) _status.Restore(user); else _status.ClearSuspension(user);
        _audit.Add(q.AdminId, q.Restore ? AdminAuditActions.UserRestored : AdminAuditActions.SuspensionCleared, "User", user.UserId, before,
            new { user.AccountStatus, user.IsActive, user.SuspendedUntil, user.BannedAt, reason = q.Reason });
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<User> GetTarget(Guid adminId, Guid userId, CancellationToken ct)
    {
        if (!await _context.Set<User>().AnyAsync(x => x.UserId == adminId && x.Role == (int)UserRole.Admin, ct)) throw new ForbiddenAccessException("Admin access is required.");
        var user = await _context.Set<User>().FirstOrDefaultAsync(x => x.UserId == userId, ct) ?? throw new NotFoundException("User does not exist.");
        if (user.Role == (int)UserRole.Admin) throw new ConflictException("Admin accounts are protected from enforcement actions.");
        return user;
    }

    private static bool IsViolationConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UserViolations_UserId_", StringComparison.OrdinalIgnoreCase) == true;
}
