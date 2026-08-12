using Application.Common.InternalServices.Accounts.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.Interfaces.Time;
using Domain.Entities;
using Domain.Enums.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.Accounts.Services;

public class UserAccountStatusService : IUserAccountStatusService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UserAccountStatusService(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public void SetActive(User user, bool isActive)
    {
        if (user.Role == (int)UserRole.Admin && !isActive)
            throw new ConflictException("Admin accounts cannot be deactivated through User management.");
        user.IsActive = isActive;
        if (isActive && user.AccountStatus != (int)AccountStatus.Banned)
            user.AccountStatus = (int)AccountStatus.Active;
        user.UpdatedAt = _dateTimeService.UtcNow;
    }

    public void Suspend(User user, DateTime suspendedUntil, string? reason)
    {
        if (user.Role == (int)UserRole.Admin)
            throw new ConflictException("Admin accounts cannot be suspended through User management.");
        var now = _dateTimeService.UtcNow;
        if (suspendedUntil <= now)
        {
            throw new BadRequestException("Suspension end time must be in the future.");
        }

        user.SuspendedAt = now;
        user.SuspendedUntil = suspendedUntil;
        user.SuspensionReason = string.IsNullOrWhiteSpace(reason)
            ? "Account temporarily suspended."
            : reason.Trim();
        user.AccountStatus = (int)AccountStatus.Suspended;
        user.IsActive = true;
        user.UpdatedAt = now;
    }

    public void Ban(User user, string reason)
    {
        if (user.Role == (int)UserRole.Admin)
            throw new ConflictException("Admin accounts cannot be banned through account enforcement.");
        var now = _dateTimeService.UtcNow;
        user.AccountStatus = (int)AccountStatus.Banned;
        user.IsActive = false;
        user.IsFlagged = true;
        user.BannedAt = now;
        user.BanReason = reason.Trim();
        user.SuspendedAt = null;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = now;
    }

    public void Restore(User user)
    {
        if (user.Role == (int)UserRole.Admin)
            throw new ConflictException("Admin accounts cannot be restored through User management.");
        user.AccountStatus = (int)AccountStatus.Active;
        user.IsActive = true;
        user.SuspendedAt = null;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        user.BannedAt = null;
        user.BanReason = null;
        user.IsFlagged = user.ViolationCount > 0;
        user.UpdatedAt = _dateTimeService.UtcNow;
    }

    public async Task<AccountEnforcementResult> ApplyViolationAsync(
        User user, AccountViolationSource source, UserViolationType violationType,
        string reason, string? description, Guid adminId,
        AccountEnforcementAction? requestedAction, DateTime? suspendedUntil,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Enforcement reason is required.");
        if (user.Role == (int)UserRole.Admin) throw new ConflictException("Admin accounts are protected from account enforcement.");
        var duplicate = source.SourceType switch
        {
            UserViolationSourceType.Dispute => await _context.Set<UserViolation>().AnyAsync(v => v.UserId == user.UserId && v.DisputeId == source.DisputeId, cancellationToken),
            UserViolationSourceType.Report => await _context.Set<UserViolation>().AnyAsync(v => v.UserId == user.UserId && v.ReportId == source.ReportId, cancellationToken),
            UserViolationSourceType.ManualAdmin => await _context.Set<UserViolation>().AnyAsync(v => v.UserId == user.UserId && v.ManualActionId == source.ManualActionId, cancellationToken),
            _ => throw new BadRequestException("Invalid violation source.")
        };
        var previousCount = user.ViolationCount;
        var previousStatus = user.AccountStatus;
        if (duplicate)
            return new(true, previousCount, user.ViolationCount, previousStatus, user.AccountStatus, null, user.SuspendedUntil, null);

        var now = _dateTimeService.UtcNow;
        user.ViolationCount++;
        user.IsFlagged = true;
        var action = requestedAction ?? (user.ViolationCount >= 3
            ? AccountEnforcementAction.PermanentBan
            : user.ViolationCount == 2 ? AccountEnforcementAction.Suspension : AccountEnforcementAction.Warning);
        DateTime? appliedSuspension = null;
        UserViolationAction violationAction;

        if (action == AccountEnforcementAction.PermanentBan)
        {
            Ban(user, reason);
            violationAction = UserViolationAction.PermanentBan;
        }
        else if (action == AccountEnforcementAction.Suspension)
        {
            appliedSuspension = suspendedUntil ?? now.AddDays(7);
            Suspend(user, appliedSuspension.Value, reason);
            violationAction = UserViolationAction.TemporarySuspension;
        }
        else
        {
            violationAction = UserViolationAction.Warning;
            if (user.AccountStatus != (int)AccountStatus.Banned &&
                !(user.AccountStatus == (int)AccountStatus.Suspended && user.SuspendedUntil > now))
            {
                user.AccountStatus = (int)AccountStatus.Active;
                user.IsActive = true;
            }
            user.UpdatedAt = now;
        }

        var violation = new UserViolation
        {
            UserViolationId = Guid.NewGuid(), UserId = user.UserId, SourceType = (int)source.SourceType,
            DisputeId = source.DisputeId, ReportId = source.ReportId, ManualActionId = source.ManualActionId,
            ContractId = source.ContractId, MilestoneId = source.MilestoneId,
            ViolationNumber = user.ViolationCount, ViolationType = (int)violationType,
            Reason = reason.Trim(), Description = description?.Trim(), ActionTaken = (int)violationAction,
            SuspendedUntil = appliedSuspension, CreatedByAdminId = adminId, CreatedAt = now, IsActive = true
        };
        _context.Set<UserViolation>().Add(violation);
        return new(false, previousCount, user.ViolationCount, previousStatus, user.AccountStatus,
            violationAction, appliedSuspension, violation.UserViolationId);
    }

    public void ClearSuspension(User user)
    {
        if (user.Role == (int)UserRole.Admin)
            throw new ConflictException("Admin accounts cannot be changed through User management.");
        user.SuspendedAt = null;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        if (user.AccountStatus != (int)AccountStatus.Banned)
            user.AccountStatus = (int)AccountStatus.Active;
        user.UpdatedAt = _dateTimeService.UtcNow;
    }

    public async Task<User?> ToggleActiveAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        SetActive(user, !user.IsActive);
        return user;
    }

    public async Task<User?> SuspendAsync(
        Guid userId,
        DateTime suspendedUntil,
        string? reason,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(existingUser => existingUser.UserId == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        Suspend(user, suspendedUntil, reason);
        return user;
    }

    public async Task<User?> SuspendAsync(
        string email,
        DateTime suspendedUntil,
        string? reason,
        CancellationToken cancellationToken)
    {
        var user = await FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        Suspend(user, suspendedUntil, reason);
        return user;
    }

    public async Task<User?> ClearSuspensionAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        ClearSuspension(user);
        return user;
    }

    private Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();
        return _context.Set<User>()
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
    }
}
