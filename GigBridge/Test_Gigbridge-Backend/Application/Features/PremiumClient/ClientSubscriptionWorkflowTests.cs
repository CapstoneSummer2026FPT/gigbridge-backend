using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Features.Premium.Client.Subscriptions.AutoRenew.Commands;
using Application.Features.Premium.Client.Subscriptions.Cancel;
using Application.Features.Premium.Client.Subscriptions.GetHistory;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Subscriptions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.PremiumClient;

public sealed class ClientSubscriptionWorkflowTests
{
    private static readonly DateTime Now =
        new(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task History_ReturnsOnlyClientCompatiblePaidSubscriptionsNewestFirst()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(
            CreateSubscription(userId, CreatePlan(UserRole.Client), Now.AddDays(-2)),
            CreateSubscription(userId, CreatePlan(null), Now.AddDays(-1)),
            CreateSubscription(userId, CreatePlan(UserRole.Freelancer), Now));
        var handler = new GetClientSubscriptionHistoryQueryHandler(context, new Clock());

        var result = await handler.Handle(
            new GetClientSubscriptionHistoryQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Shared Premium", result[0].PlanName);
        Assert.Equal("Client Premium", result[1].PlanName);
    }

    [Fact]
    public async Task Cancel_DisablesRenewalButPreservesCurrentEntitlement()
    {
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, CreatePlan(UserRole.Client), Now.AddDays(-1));
        subscription.AutoRenew = true;
        var context = new InMemoryApplicationDbContext();
        context.AddSet(subscription);
        var cache = new RecordingCache();
        var handler = new CancelClientSubscriptionCommandHandler(
            context, new Clock(), cache, new NoopNotificationService());

        var result = await handler.Handle(
            new CancelClientSubscriptionCommand(userId), CancellationToken.None);

        Assert.False(result.AutoRenew);
        Assert.True(result.IsPremium);
        Assert.Equal(Now, result.CancelledAt);
        Assert.Equal(1, context.SaveChangesCount);
        Assert.Contains($"premium:access:client:{userId:N}", cache.RemovedKeys);
    }

    [Fact]
    public async Task AutoRenew_ReenableClearsCancellationTimestamp()
    {
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, CreatePlan(UserRole.Client), Now.AddDays(-1));
        subscription.AutoRenew = false;
        subscription.CancelledAt = Now.AddHours(-1);
        var context = new InMemoryApplicationDbContext();
        context.AddSet(subscription);
        var cache = new RecordingCache();
        var handler = new UpdateClientSubscriptionAutoRenewCommandHandler(
            context, new Clock(), cache);

        var result = await handler.Handle(
            new UpdateClientSubscriptionAutoRenewCommand(userId, true), CancellationToken.None);

        Assert.True(result.AutoRenew);
        Assert.Null(result.CancelledAt);
        Assert.Equal(Now, subscription.UpdatedAt);
        Assert.Contains($"premium:access:client:{userId:N}", cache.RemovedKeys);
    }

    [Fact]
    public async Task AutoRenew_RejectsFreelancerSubscriptionForClientFlow()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(CreateSubscription(
            userId, CreatePlan(UserRole.Freelancer), Now.AddDays(-1)));
        var handler = new UpdateClientSubscriptionAutoRenewCommandHandler(
            context, new Clock(), new RecordingCache());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateClientSubscriptionAutoRenewCommand(userId, true),
            CancellationToken.None));
    }

    [Fact]
    public void Commands_RejectEmptyUserIds()
    {
        var cancelResult = new CancelClientSubscriptionCommandValidator()
            .Validate(new CancelClientSubscriptionCommand(Guid.Empty));
        var autoRenewResult = new UpdateClientSubscriptionAutoRenewCommandValidator()
            .Validate(new UpdateClientSubscriptionAutoRenewCommand(Guid.Empty, true));

        Assert.Contains(cancelResult.Errors, error => error.PropertyName == "UserId");
        Assert.Contains(autoRenewResult.Errors, error => error.PropertyName == "UserId");
    }

    private static SubscriptionPlan CreatePlan(UserRole? role) => new()
    {
        SubscriptionPlansId = Guid.NewGuid(),
        Name = role switch
        {
            UserRole.Client => "Client Premium",
            UserRole.Freelancer => "Freelancer Premium",
            _ => "Shared Premium"
        },
        Price = 500m,
        Currency = "GigCoin",
        DurationInDays = 30,
        TargetRole = role is null ? null : (int)role,
        IsActive = true
    };

    private static Subscription CreateSubscription(
        Guid userId, SubscriptionPlan plan, DateTime createdAt) => new()
    {
        SubscriptionsId = Guid.NewGuid(),
        UserId = userId,
        SubscriptionPlansId = plan.SubscriptionPlansId,
        SubscriptionPlans = plan,
        Status = SubscriptionStatus.Active,
        StartDate = Now.AddDays(-10),
        EndDate = Now.AddDays(20),
        AutoRenew = true,
        CreatedAt = createdAt
    };

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }

    private sealed class RecordingCache : ICacheService
    {
        public List<string> RemovedKeys { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }
}
