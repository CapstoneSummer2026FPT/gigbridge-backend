using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Users.Premium.Grant.Commands;
using Application.Features.Admin.Users.Premium.Revoke.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Subscriptions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Users;

public sealed class AdminPremiumCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(UserRole.Client, "premium:access:client:")]
    [InlineData(UserRole.Freelancer, "premium:access:")]
    public async Task Grant_CreatesRoleCompatiblePremiumAndInvalidatesRoleCache(
        UserRole role,
        string expectedCachePrefix)
    {
        await using var context = CreateContext();
        var user = AddUser(context, role);
        var correctPlan = AddPlan(context, role, 365);
        AddPlan(context, role == UserRole.Client ? UserRole.Freelancer : UserRole.Client, 730);
        await context.SaveChangesAsync();
        var cache = Substitute.For<ICacheService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new GrantUserPremiumCommandHandler(context, new Clock(), cache, notifications);

        var changed = await handler.Handle(new GrantUserPremiumCommand(user.UserId), CancellationToken.None);

        Assert.True(changed);
        var subscription = await context.Subscriptions.SingleAsync();
        Assert.Equal(correctPlan.SubscriptionPlansId, subscription.SubscriptionPlansId);
        Assert.Equal(Now.AddDays(365), subscription.EndDate);
        await cache.Received(1).RemoveAsync(
            $"{expectedCachePrefix}{user.UserId:N}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_CancelsClientPremiumAndInvalidatesClientCache()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Client);
        var plan = AddPlan(context, UserRole.Client, 30);
        var subscription = AddSubscription(context, user, plan);
        await context.SaveChangesAsync();
        var cache = Substitute.For<ICacheService>();
        var handler = new RevokeUserPremiumCommandHandler(
            context, new Clock(), cache, Substitute.For<INotificationService>());

        var changed = await handler.Handle(new RevokeUserPremiumCommand(user.UserId), CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Equal(Now, subscription.EndDate);
        Assert.Equal(Now, subscription.CancelledAt);
        Assert.False(subscription.AutoRenew);
        await cache.Received(1).RemoveAsync(
            $"premium:access:client:{user.UserId:N}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grant_RejectsAdminAccount()
    {
        await using var context = CreateContext();
        var admin = AddUser(context, UserRole.Admin);
        await context.SaveChangesAsync();
        var handler = new GrantUserPremiumCommandHandler(
            context,
            new Clock(),
            Substitute.For<ICacheService>(),
            Substitute.For<INotificationService>());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new GrantUserPremiumCommand(admin.UserId), CancellationToken.None));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static User AddUser(GigbridgeDbContext context, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = $"{role} User",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = (int)role,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = Now
        };
        context.Users.Add(user);
        return user;
    }

    private static SubscriptionPlan AddPlan(
        GigbridgeDbContext context,
        UserRole role,
        int durationInDays)
    {
        var plan = new SubscriptionPlan
        {
            SubscriptionPlansId = Guid.NewGuid(),
            Name = $"{role} Premium",
            Price = 500m,
            Currency = "GigCoin",
            DurationInDays = durationInDays,
            TargetRole = (int)role,
            IsActive = true,
            CreatedAt = Now
        };
        context.SubscriptionPlans.Add(plan);
        return plan;
    }

    private static Subscription AddSubscription(
        GigbridgeDbContext context,
        User user,
        SubscriptionPlan plan)
    {
        var subscription = new Subscription
        {
            SubscriptionsId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            SubscriptionPlansId = plan.SubscriptionPlansId,
            SubscriptionPlans = plan,
            Status = SubscriptionStatus.Active,
            StartDate = Now.AddDays(-1),
            EndDate = Now.AddDays(29),
            AutoRenew = true,
            CreatedAt = Now
        };
        context.Subscriptions.Add(subscription);
        return subscription;
    }

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }
}
