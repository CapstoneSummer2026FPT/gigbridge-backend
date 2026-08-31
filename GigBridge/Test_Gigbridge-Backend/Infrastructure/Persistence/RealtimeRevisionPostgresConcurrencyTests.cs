using Application.Common.InternalServices.Realtime.Services;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Test_Gigbridge_Backend.Infrastructure.Persistence;

public sealed class RealtimeRevisionPostgresConcurrencyTests
{
    [PostgresIntegrationFact]
    public async Task SharedRevisionLocks_SerializeConcurrentSavesAndPreserveTransactions()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        await ConcurrentConversationChangesAdvanceEveryParticipant(connectionString);
        await ConcurrentFirstWritesShareOneUserRealtimeState(connectionString);
        await FailedSaveRollsBackRealtimeStateAndOutbox(connectionString);
        await ExistingTransactionRemainsOwnedByCaller(connectionString);
    }

    private static async Task ConcurrentConversationChangesAdvanceEveryParticipant(string connectionString)
    {
        await RecreateDatabase(connectionString);
        var seeded = await SeedConversation(connectionString, participantCount: 2, "conversation-race");
        await using var first = CreateContext(connectionString, withInterceptor: true);
        await using var second = CreateContext(connectionString, withInterceptor: true);
        (await first.Conversations.SingleAsync()).UpdatedAt = DateTime.UtcNow.AddSeconds(1);
        (await second.Conversations.SingleAsync()).UpdatedAt = DateTime.UtcNow.AddSeconds(2);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSave = SaveAfterSignal(first, start.Task);
        var secondSave = SaveAfterSignal(second, start.Task);
        start.SetResult();
        await Task.WhenAll(firstSave, secondSave);

        await using var verify = CreateContext(connectionString, withInterceptor: false);
        var states = await verify.UserRealtimeStates.AsNoTracking()
            .Where(state => seeded.UserIds.Contains(state.UserId))
            .ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state => Assert.Equal(2, state.ConversationRevision));

        var deliveries = await verify.DeliveryOutboxes.AsNoTracking()
            .Where(delivery =>
                seeded.UserIds.Contains(delivery.RecipientUserId) &&
                delivery.DeliveryType == (int)DeliveryOutboxType.ConversationInboxRevision)
            .ToListAsync();
        Assert.Equal(4, deliveries.Count);
        Assert.Equal(4, deliveries.Select(delivery => delivery.DeliveryKey).Distinct().Count());
        Assert.All(
            deliveries.GroupBy(delivery => delivery.RecipientUserId),
            group => Assert.Equal([1, 2], group.Select(item => item.EventSequence).Order()));
    }

    private static async Task ConcurrentFirstWritesShareOneUserRealtimeState(string connectionString)
    {
        await RecreateDatabase(connectionString);
        var seeded = await SeedConversation(connectionString, participantCount: 1, "first-state-race");
        var userId = Assert.Single(seeded.UserIds);
        await using var notificationContext = CreateContext(connectionString, withInterceptor: true);
        await using var conversationContext = CreateContext(connectionString, withInterceptor: true);
        notificationContext.Notifications.Add(new Notification
        {
            NotificationsId = Guid.NewGuid(),
            UserId = userId,
            Type = 0,
            Title = "Concurrent notification",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        (await conversationContext.Conversations.SingleAsync()).UpdatedAt = DateTime.UtcNow.AddSeconds(1);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationSave = SaveAfterSignal(notificationContext, start.Task);
        var conversationSave = SaveAfterSignal(conversationContext, start.Task);
        start.SetResult();
        await Task.WhenAll(notificationSave, conversationSave);

        await using var verify = CreateContext(connectionString, withInterceptor: false);
        var state = await verify.UserRealtimeStates.AsNoTracking().SingleAsync(item => item.UserId == userId);
        Assert.Equal(1, state.NotificationRevision);
        Assert.Equal(1, state.ConversationRevision);
        Assert.Equal(2, await verify.DeliveryOutboxes.CountAsync(item => item.RecipientUserId == userId));
    }

    private static async Task FailedSaveRollsBackRealtimeStateAndOutbox(string connectionString)
    {
        await RecreateDatabase(connectionString);
        var seeded = await SeedConversation(connectionString, participantCount: 1, "rollback");
        var userId = Assert.Single(seeded.UserIds);
        await using var context = CreateContext(connectionString, withInterceptor: true);
        var conversation = await context.Conversations.SingleAsync();
        var originalUpdatedAt = conversation.UpdatedAt;
        conversation.UpdatedAt = DateTime.UtcNow.AddMinutes(1);
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Duplicate primary key",
            Email = "duplicate-primary-key@example.test"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await using var verify = CreateContext(connectionString, withInterceptor: false);
        Assert.Equal(originalUpdatedAt, (await verify.Conversations.AsNoTracking().SingleAsync()).UpdatedAt);
        Assert.False(await verify.UserRealtimeStates.AnyAsync(item => item.UserId == userId));
        Assert.False(await verify.DeliveryOutboxes.AnyAsync(item => item.RecipientUserId == userId));
    }

    private static async Task ExistingTransactionRemainsOwnedByCaller(string connectionString)
    {
        await RecreateDatabase(connectionString);
        var seeded = await SeedConversation(connectionString, participantCount: 1, "caller-transaction");
        var userId = Assert.Single(seeded.UserIds);
        await using var context = CreateContext(connectionString, withInterceptor: true);
        await using var transaction = await context.BeginTransactionAsync(CancellationToken.None);
        (await context.Conversations.SingleAsync()).UpdatedAt = DateTime.UtcNow.AddSeconds(1);

        await context.SaveChangesAsync();

        await using (var beforeCommit = CreateContext(connectionString, withInterceptor: false))
        {
            Assert.False(await beforeCommit.UserRealtimeStates.AnyAsync(item => item.UserId == userId));
        }

        await transaction.CommitAsync(CancellationToken.None);

        await using var afterCommit = CreateContext(connectionString, withInterceptor: false);
        Assert.True(await afterCommit.UserRealtimeStates.AnyAsync(item => item.UserId == userId));
    }

    private static async Task SaveAfterSignal(GigbridgeDbContext context, Task signal)
    {
        await signal;
        await context.SaveChangesAsync();
    }

    private static async Task RecreateDatabase(string connectionString)
    {
        await using var context = CreateContext(connectionString, withInterceptor: false);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private static async Task<SeededConversation> SeedConversation(
        string connectionString,
        int participantCount,
        string name)
    {
        await using var context = CreateContext(connectionString, withInterceptor: false);
        var conversationId = Guid.NewGuid();
        var users = Enumerable.Range(0, participantCount)
            .Select(index => new User
            {
                UserId = Guid.NewGuid(),
                FullName = $"{name} user {index}",
                Email = $"{name}-{index}@example.test"
            })
            .ToArray();
        context.Users.AddRange(users);
        context.Conversations.Add(new Conversation
        {
            ConversationsId = conversationId,
            ConversationType = (int)ConversationType.JobNegotiation,
            CreatedByUserId = users[0].UserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.ConversationParticipants.AddRange(users.Select((user, index) =>
            new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversationId,
                UserId = user.UserId,
                ParticipantRole = index == 0
                    ? (int)ParticipantRole.Client
                    : (int)ParticipantRole.Freelancer,
                JoinedAt = DateTime.UtcNow
            }));
        await context.SaveChangesAsync();
        return new SeededConversation(conversationId, users.Select(user => user.UserId).ToArray());
    }

    private static GigbridgeDbContext CreateContext(string connectionString, bool withInterceptor)
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseNpgsql(connectionString);
        if (withInterceptor)
        {
            options.AddInterceptors(new RealtimeRevisionSaveChangesInterceptor());
        }

        return new GigbridgeDbContext(options.Options);
    }

    private sealed record SeededConversation(Guid ConversationId, Guid[] UserIds);
}

public sealed class PostgresIntegrationFactAttribute : FactAttribute
{
    public PostgresIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GIGBRIDGE_RUN_POSTGRES_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set GIGBRIDGE_RUN_POSTGRES_TESTS=1 and start Docker to run PostgreSQL concurrency tests.";
        }
    }
}
