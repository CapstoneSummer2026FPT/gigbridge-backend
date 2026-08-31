using Application.Common.InternalServices.Accounts.Models;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Domain.Entities;
using Domain.Enums.Accounts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Accounts.Services;

public sealed class UserAccountStatusServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DisputeViolations_ApplyWarningSuspensionAndPermanentBanTiers()
    {
        await using var context = CreateContext();
        var user = User("freelancer@example.test", UserRole.Freelancer);
        user.RefreshTokenHash = "current-refresh-hash";
        user.RefreshTokenExpiry = Now.AddDays(7);
        user.PreviousRefreshTokenHash = "previous-refresh-hash";
        user.PreviousRefreshTokenGraceExpiresAt = Now.AddSeconds(30);
        var admin = User("admin@example.test", UserRole.Admin);
        context.Users.AddRange(user, admin); await context.SaveChangesAsync();
        var service = new UserAccountStatusService(context, new Clock());

        var first = await Apply(service, user, admin.UserId, 1);
        await context.SaveChangesAsync();
        Assert.Equal(UserViolationAction.Warning, first.Action);
        Assert.True(user.IsFlagged); Assert.Equal((int)AccountStatus.Active, user.AccountStatus);

        var second = await Apply(service, user, admin.UserId, 2);
        await context.SaveChangesAsync();
        Assert.Equal(UserViolationAction.TemporarySuspension, second.Action);
        Assert.Equal(Now.AddDays(7), user.SuspendedUntil);

        var third = await Apply(service, user, admin.UserId, 3);
        await context.SaveChangesAsync();
        Assert.Equal(UserViolationAction.PermanentBan, third.Action);
        Assert.False(user.IsActive); Assert.Equal((int)AccountStatus.Banned, user.AccountStatus);
        Assert.Equal(3, user.ViolationCount);
        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Null(user.PreviousRefreshTokenHash);
        Assert.Null(user.PreviousRefreshTokenGraceExpiresAt);
    }

    [Fact]
    public async Task ManualRequestId_IsIdempotent_AndRestorePreservesLifetimeHistory()
    {
        await using var context = CreateContext(); var user = User("client@example.test", UserRole.Client); var admin = User("admin@example.test", UserRole.Admin);
        context.Users.AddRange(user, admin); await context.SaveChangesAsync(); var service = new UserAccountStatusService(context, new Clock()); var requestId = Guid.NewGuid();
        var source = new AccountViolationSource(UserViolationSourceType.ManualAdmin, ManualActionId: requestId);
        await service.ApplyViolationAsync(user, source, UserViolationType.Other, "Confirmed", null, admin.UserId, AccountEnforcementAction.PermanentBan, null, default); await context.SaveChangesAsync();
        var duplicate = await service.ApplyViolationAsync(user, source, UserViolationType.Other, "Retry", null, admin.UserId, AccountEnforcementAction.PermanentBan, null, default);
        service.Restore(user); await context.SaveChangesAsync();

        Assert.True(duplicate.Duplicate); Assert.Equal(1, user.ViolationCount); Assert.True(user.IsFlagged);
        Assert.True(user.IsActive); Assert.Equal((int)AccountStatus.Active, user.AccountStatus);
        Assert.Single(await context.UserViolations.ToListAsync());
    }

    private static Task<AccountEnforcementResult> Apply(UserAccountStatusService service, User user, Guid adminId, int number) =>
        service.ApplyViolationAsync(user, new(UserViolationSourceType.Dispute, DisputeId: Guid.NewGuid(), ContractId: Guid.NewGuid()),
            UserViolationType.ContractBreach, $"Violation {number}", null, adminId, null, null, default);
    private static GigbridgeDbContext CreateContext() => new(new DbContextOptionsBuilder<GigbridgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User User(string email, UserRole role) => new() { UserId = Guid.NewGuid(), Email = email, FullName = email, Password = "none", Role = (int)role, IsActive = true, AccountStatus = (int)AccountStatus.Active, CreatedAt = Now };
    private sealed class Clock : IDateTimeService { public DateTime UtcNow => Now; }
}
