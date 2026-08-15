using Application.Common.InternalServices.Premium.Models;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Premium.Services;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Exceptions;
using Application.Features.Premium.Client.Subscriptions.GetCurrent;
using Application.Features.Premium.Client.Subscriptions.Purchase;
using Application.Features.Premium.Common;
using Application.Features.Subscriptions.Common;
using Application.Features.Subscriptions.Freelancer.GetCurrent;
using Application.Features.Subscriptions.Freelancer.Purchase;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Subscriptions;
using Domain.Enums.Wallets;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Subscriptions;

public sealed class PremiumPurchaseWorkflowTests
{
    private static readonly DateTime Now =
        new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ClientPurchase_IgnoresWrongRoleSubscription_AndActivatesImmediately()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserRole.Client);
        var freelancerPlan = CreatePlan(UserRole.Freelancer, "Freelancer Premium");
        var clientPlan = CreatePlan(UserRole.Client, "Client Premium");
        var wrongRoleSubscription = CreateSubscription(
            userId, freelancerPlan, Now.AddDays(-5), Now.AddDays(25));
        var context = CreateContext(user, clientPlan, freelancerPlan, wrongRoleSubscription);
        var ledger = new Ledger(context);
        var cache = new MemoryCache();
        var handler = new PurchaseClientSubscriptionCommandHandler(
            context, ledger, new Clock(), cache, new NoopNotificationService());

        var purchased = await handler.Handle(
            new PurchaseClientSubscriptionCommand(userId,
                new PurchaseSubscriptionRequest(clientPlan.SubscriptionPlansId, "client-purchase-1")),
            CancellationToken.None);

        Assert.Equal(Now, purchased.StartDate);
        Assert.Equal(Now.AddDays(clientPlan.DurationInDays), purchased.EndDate);
        Assert.Equal(PremiumSubscriptionPolicy.PurchaseLockKey(userId),
            context.LastTransactionLockKey);
        Assert.Equal(1, context.TransactionCommitCount);

        // Simulate another backend instance that still has a cached negative result.
        var accessCache = new MemoryCache();
        await accessCache.SetAsync($"premium:access:client:{userId:N}",
            new PremiumBenefitsDto(false, true, false, null, null));
        var access = new PremiumAccessService(context, accessCache, new Clock());
        Assert.True(await access.IsPremiumClientAsync(userId, CancellationToken.None));

        var current = await new GetCurrentClientSubscriptionQueryHandler(context, new Clock())
            .Handle(new GetCurrentClientSubscriptionQuery(userId), CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(purchased.Id, current!.Id);
    }

    [Fact]
    public async Task ClientPurchase_WithCurrentCompatibleQueue_AppendsAfterLatestEnd()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserRole.Client);
        var clientPlan = CreatePlan(UserRole.Client, "Client Premium Monthly");
        var sharedPlan = CreatePlan(null, "Shared Premium");
        var current = CreateSubscription(
            userId, clientPlan, Now.AddDays(-5), Now.AddDays(10));
        var queued = CreateSubscription(
            userId, sharedPlan, current.EndDate, Now.AddDays(40));
        var context = CreateContext(user, clientPlan, sharedPlan, current, queued);
        var handler = new PurchaseClientSubscriptionCommandHandler(
            context, new Ledger(context), new Clock(), new MemoryCache(),
            new NoopNotificationService());

        var purchased = await handler.Handle(
            new PurchaseClientSubscriptionCommand(userId,
                new PurchaseSubscriptionRequest(clientPlan.SubscriptionPlansId, "client-purchase-2")),
            CancellationToken.None);

        Assert.Equal(queued.EndDate, purchased.StartDate);
        Assert.Equal(queued.EndDate.AddDays(clientPlan.DurationInDays), purchased.EndDate);
    }

    [Fact]
    public async Task ClientPurchase_WithOnlyFutureOrInvalidSubscriptions_ActivatesImmediately()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserRole.Client);
        var clientPlan = CreatePlan(UserRole.Client, "Client Premium");
        var inactivePlan = CreatePlan(UserRole.Client, "Inactive Client Premium");
        inactivePlan.IsActive = false;
        var freePlan = CreatePlan(UserRole.Client, "Client Free");
        freePlan.Price = 0;
        var future = CreateSubscription(
            userId, clientPlan, Now.AddDays(20), Now.AddDays(50));
        var inactive = CreateSubscription(
            userId, inactivePlan, Now.AddDays(-1), Now.AddDays(20));
        var free = CreateSubscription(
            userId, freePlan, Now.AddDays(-1), Now.AddDays(20));
        var cancelled = CreateSubscription(
            userId, clientPlan, Now.AddDays(-1), Now.AddDays(20));
        cancelled.Status = SubscriptionStatus.Cancelled;
        var expired = CreateSubscription(
            userId, clientPlan, Now.AddDays(-30), Now.AddDays(-1));
        expired.Status = SubscriptionStatus.Expired;
        var context = CreateContext(
            user, clientPlan, inactivePlan, freePlan, future, inactive, free, cancelled, expired);
        var handler = new PurchaseClientSubscriptionCommandHandler(
            context, new Ledger(context), new Clock(), new MemoryCache(),
            new NoopNotificationService());

        var purchased = await handler.Handle(
            new PurchaseClientSubscriptionCommand(userId,
                new PurchaseSubscriptionRequest(clientPlan.SubscriptionPlansId, "client-purchase-3")),
            CancellationToken.None);

        Assert.Equal(Now, purchased.StartDate);
    }

    [Theory]
    [InlineData(false, 500, (int)UserRole.Client)]
    [InlineData(true, 0, (int)UserRole.Client)]
    [InlineData(true, 500, (int)UserRole.Freelancer)]
    public async Task ClientPurchase_RejectsInactiveFreeOrWrongRolePlan(
        bool isActive,
        decimal price,
        int targetRole)
    {
        var userId = Guid.NewGuid();
        var plan = CreatePlan((UserRole)targetRole, "Invalid Client Plan");
        plan.IsActive = isActive;
        plan.Price = price;
        var context = CreateContext(CreateUser(userId, UserRole.Client), plan);
        var ledger = new Ledger(context);
        var handler = new PurchaseClientSubscriptionCommandHandler(
            context, ledger, new Clock(), new MemoryCache(),
            new NoopNotificationService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new PurchaseClientSubscriptionCommand(userId,
                new PurchaseSubscriptionRequest(plan.SubscriptionPlansId, "invalid-client-plan")),
            CancellationToken.None));

        Assert.Equal(0, ledger.DebitCount);
        Assert.Empty(context.Set<Subscription>());
    }

    [Fact]
    public async Task FreelancerPurchase_AcceptsSharedPlan_IsImmediate_AndIsIdempotent()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserRole.Freelancer);
        var sharedPlan = CreatePlan(null, "Shared Premium");
        var context = CreateContext(user, sharedPlan);
        var ledger = new Ledger(context);
        var handler = new PurchaseSubscriptionCommandHandler(
            context, ledger, new Clock(), new MemoryCache(),
            new NoopNotificationService());
        var command = new PurchaseSubscriptionCommand(userId,
            new PurchaseSubscriptionRequest(sharedPlan.SubscriptionPlansId,
                "freelancer-purchase-1"));

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(Now, first.StartDate);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, ledger.DebitCount);
        Assert.Single(context.Set<Subscription>());
        Assert.True(await new PremiumAccessService(
                context, new MemoryCache(), new Clock())
            .IsPremiumFreelancerAsync(userId, CancellationToken.None));

        var current = await new GetCurrentSubscriptionQueryHandler(context, new Clock())
            .Handle(new GetCurrentSubscriptionQuery(userId), CancellationToken.None);
        Assert.Equal(first.Id, current!.Id);
    }

    [Theory]
    [InlineData(false, 500, (int)UserRole.Freelancer)]
    [InlineData(true, 0, (int)UserRole.Freelancer)]
    [InlineData(true, 500, (int)UserRole.Client)]
    public async Task FreelancerPurchase_RejectsInactiveFreeOrWrongRolePlan(
        bool isActive,
        decimal price,
        int targetRole)
    {
        var userId = Guid.NewGuid();
        var plan = CreatePlan((UserRole)targetRole, "Invalid Freelancer Plan");
        plan.IsActive = isActive;
        plan.Price = price;
        var context = CreateContext(CreateUser(userId, UserRole.Freelancer), plan);
        var ledger = new Ledger(context);
        var handler = new PurchaseSubscriptionCommandHandler(
            context, ledger, new Clock(), new MemoryCache(),
            new NoopNotificationService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new PurchaseSubscriptionCommand(userId,
                new PurchaseSubscriptionRequest(plan.SubscriptionPlansId, "invalid-freelancer-plan")),
            CancellationToken.None));

        Assert.Equal(0, ledger.DebitCount);
        Assert.Empty(context.Set<Subscription>());
    }

    [Fact]
    public void PurchaseLock_IsStablePerUser_AndIsolatedBetweenUsers()
    {
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        Assert.Equal(PremiumSubscriptionPolicy.PurchaseLockKey(firstUser),
            PremiumSubscriptionPolicy.PurchaseLockKey(firstUser));
        Assert.NotEqual(PremiumSubscriptionPolicy.PurchaseLockKey(firstUser),
            PremiumSubscriptionPolicy.PurchaseLockKey(secondUser));
    }

    private static InMemoryApplicationDbContext CreateContext(
        User user,
        SubscriptionPlan firstPlan,
        params object[] remaining)
    {
        var plans = new List<SubscriptionPlan> { firstPlan };
        var subscriptions = new List<Subscription>();
        foreach (var item in remaining)
        {
            if (item is SubscriptionPlan plan)
                plans.Add(plan);
            else if (item is Subscription subscription)
                subscriptions.Add(subscription);
        }

        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        context.AddSet(plans.ToArray());
        context.AddSet(subscriptions.ToArray());
        context.AddSet<WalletTransaction>();
        return context;
    }

    private static User CreateUser(Guid userId, UserRole role) => new()
    {
        UserId = userId,
        FullName = role.ToString(),
        Email = $"{role.ToString().ToLowerInvariant()}@example.com",
        Role = (int)role,
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = Now
    };

    private static SubscriptionPlan CreatePlan(UserRole? role, string name) => new()
    {
        SubscriptionPlansId = Guid.NewGuid(),
        Name = name,
        Price = 500m,
        Currency = "GigCoin",
        DurationInDays = 30,
        TargetRole = role is null ? null : (int)role,
        IsActive = true,
        CreatedAt = Now
    };

    private static Subscription CreateSubscription(
        Guid userId,
        SubscriptionPlan plan,
        DateTime startsAt,
        DateTime endsAt) => new()
    {
        SubscriptionsId = Guid.NewGuid(),
        UserId = userId,
        SubscriptionPlansId = plan.SubscriptionPlansId,
        SubscriptionPlans = plan,
        Status = SubscriptionStatus.Active,
        StartDate = startsAt,
        EndDate = endsAt,
        AutoRenew = false,
        CreatedAt = Now
    };

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }

    private sealed class Ledger(InMemoryApplicationDbContext context)
        : IWalletLedgerService
    {
        public int DebitCount { get; private set; }

        public Task<WalletTransaction> DebitAsync(
            Guid userId,
            decimal tokenAmount,
            WalletTransactionType type,
            string idempotencyKey,
            string? metadata,
            CancellationToken cancellationToken)
        {
            DebitCount++;
            var transaction = new WalletTransaction
            {
                WalletTransactionsId = Guid.NewGuid(),
                UserWalletsId = Guid.NewGuid(),
                UserId = userId,
                TokenAmount = tokenAmount,
                Type = (int)type,
                Status = (int)WalletTransactionStatus.Succeeded,
                IdempotencyKey = idempotencyKey,
                Metadata = metadata,
                CreatedAt = Now,
                CompletedAt = Now
            };
            context.Set<WalletTransaction>().Add(transaction);
            return Task.FromResult(transaction);
        }
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new();

        public Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value)
                ? (T?)value
                : default);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            _values[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
