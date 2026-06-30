using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Services;

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
        user.IsActive = isActive;
        user.UpdatedAt = _dateTimeService.UtcNow;
    }

    public void Suspend(User user, DateTime suspendedUntil, string? reason)
    {
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
        user.UpdatedAt = now;
    }

    public void ClearSuspension(User user)
    {
        user.SuspendedAt = null;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
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
