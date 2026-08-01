using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Services;

public static class UserAccountEnforcement
{
    public static bool NormalizeExpiredSuspension(User user, DateTime now)
    {
        if (user.AccountStatus != (int)AccountStatus.Suspended ||
            !user.SuspendedUntil.HasValue || user.SuspendedUntil.Value > now)
            return false;

        user.AccountStatus = (int)AccountStatus.Active;
        user.SuspendedAt = null;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        user.UpdatedAt = now;
        return true;
    }

    public static void EnsureCanAuthenticate(User user, DateTime now)
    {
        if (string.Equals(user.Provider, "System", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("System accounts cannot authenticate interactively.");
        NormalizeExpiredSuspension(user, now);
        if (user.AccountStatus == (int)AccountStatus.Banned || !user.IsActive)
            throw new UnauthorizedAccessException("Your account has been permanently banned.");
        if (user.AccountStatus == (int)AccountStatus.Suspended &&
            user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > now)
            throw new UnauthorizedAccessException($"Your account is suspended until {user.SuspendedUntil.Value:O}");
    }
}
