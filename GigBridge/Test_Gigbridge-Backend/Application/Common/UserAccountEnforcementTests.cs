using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Application.Common;

public sealed class UserAccountEnforcementTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExpiredSuspension_ReturnsToActiveAndPreservesViolationEvidence()
    {
        var user = new User
        {
            AccountStatus = (int)AccountStatus.Suspended,
            IsActive = true,
            IsFlagged = true,
            ViolationCount = 2,
            SuspendedAt = Now.AddDays(-8),
            SuspendedUntil = Now.AddMinutes(-1),
            SuspensionReason = "Second confirmed violation"
        };

        var changed = UserAccountEnforcement.NormalizeExpiredSuspension(user, Now);

        Assert.True(changed);
        Assert.Equal((int)AccountStatus.Active, user.AccountStatus);
        Assert.Null(user.SuspendedAt);
        Assert.Null(user.SuspendedUntil);
        Assert.Null(user.SuspensionReason);
        Assert.True(user.IsFlagged);
        Assert.Equal(2, user.ViolationCount);
    }

    [Fact]
    public void ActiveSuspension_IsRejected()
    {
        var user = new User
        {
            AccountStatus = (int)AccountStatus.Suspended,
            IsActive = true,
            SuspendedUntil = Now.AddDays(1)
        };

        Assert.Throws<UnauthorizedAccessException>(() =>
            UserAccountEnforcement.EnsureCanAuthenticate(user, Now));
    }

    [Fact]
    public void PermanentBan_IsRejectedEvenWithoutSuspensionDates()
    {
        var user = new User
        {
            AccountStatus = (int)AccountStatus.Banned,
            IsActive = false,
            ViolationCount = 3,
            IsFlagged = true
        };

        Assert.Throws<UnauthorizedAccessException>(() =>
            UserAccountEnforcement.EnsureCanAuthenticate(user, Now));
    }

    [Fact]
    public void SystemProviderAccount_IsAlwaysRejectedFromInteractiveAuthentication()
    {
        var user = new User { Provider = "System", AccountStatus = (int)AccountStatus.Active, IsActive = true };

        var error = Assert.Throws<UnauthorizedAccessException>(() =>
            UserAccountEnforcement.EnsureCanAuthenticate(user, Now));

        Assert.Contains("System accounts", error.Message);
    }
}
