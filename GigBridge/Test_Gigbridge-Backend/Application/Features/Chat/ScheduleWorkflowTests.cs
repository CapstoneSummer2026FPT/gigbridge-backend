using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Messages;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Schedules;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Chat;

public class ScheduleWorkflowTests
{
    [Fact]
    public async Task Create_PersistsNeutralPermissionsAndPersonalizesRealtimePayloads()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), notifier);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddHours(2)))), default);

        var persistedMessage = await db.Messages.SingleAsync();
        var persistedEvent = System.Text.Json.JsonSerializer.Deserialize<ScheduleEventResponse>(
            persistedMessage.Metadata!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(persistedEvent);
        Assert.False(persistedEvent.CanEdit);
        Assert.False(persistedEvent.CanCancel);

        var creatorMessage = Assert.IsType<MessageResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.ClientId && x.EventName == "ReceiveMessage").Payload);
        var freelancerMessage = Assert.IsType<MessageResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.FreelancerId && x.EventName == "ReceiveMessage").Payload);
        Assert.True(creatorMessage.Schedule!.CanEdit);
        Assert.False(freelancerMessage.Schedule!.CanEdit);
        Assert.False(creatorMessage.Schedule.CanCancel);
        Assert.False(freelancerMessage.Schedule.CanCancel);

        var creatorChanged = Assert.IsType<ScheduleEventResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.ClientId && x.EventName == "ScheduleChanged").Payload);
        var freelancerChanged = Assert.IsType<ScheduleEventResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.FreelancerId && x.EventName == "ScheduleChanged").Payload);
        Assert.True(creatorChanged.CanEdit);
        Assert.False(freelancerChanged.CanEdit);
        Assert.True(created.Message.Schedule!.CanEdit);

        Assert.True(MessageHelpers.ParseScheduleMetadata(persistedMessage, fixture.ClientId, now)!.CanEdit);
        Assert.False(MessageHelpers.ParseScheduleMetadata(persistedMessage, fixture.FreelancerId, now)!.CanEdit);
    }

    [Fact]
    public async Task CreatorGrace_AllowsSameDayCancellation_WithoutConsumingEditQuota()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc); // 13:00 ICT
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), new NoopChatRealtimeNotifier());
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, " Review  call ", null, new DateTimeOffset(now.AddHours(2)))), default);

        var cancelled = await handler.Handle(new CancelScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new CancelScheduleRequest("Wrong time", created.Schedule.Version)), default);

        Assert.Equal((int)ScheduleStatus.Cancelled, cancelled.Schedule.Status);
        Assert.Equal(0, cancelled.Schedule.EditCount);
        Assert.Equal(2, cancelled.Schedule.RemainingEdits);
    }

    [Fact]
    public async Task CanonicalNoOp_DoesNotConsumeEditQuota()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), new NoopChatRealtimeNotifier());
        var starts = new DateTimeOffset(now.AddDays(2));
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review call", "Line one\r\nLine two", starts)), default);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new UpdateScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new UpdateScheduleRequest(" Review   call ", "Line one\nLine two", starts, 1)), default));
        Assert.Equal(0, (await db.Schedules.SingleAsync()).EditCount);
    }

    [Fact]
    public async Task SharedQuota_RejectsThirdEdit()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), new NoopChatRealtimeNotifier());
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        var one = await handler.Handle(new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new UpdateScheduleRequest("Review one", null, new DateTimeOffset(now.AddDays(3)), 1)), default);
        var two = await handler.Handle(new UpdateScheduleCommand(fixture.FreelancerId, created.Schedule.ScheduleId,
            new UpdateScheduleRequest("Review two", null, new DateTimeOffset(now.AddDays(3)), one.Schedule.Version)), default);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new UpdateScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new UpdateScheduleRequest("Review three", null, new DateTimeOffset(now.AddDays(3)), two.Schedule.Version)), default));
    }

    [Fact]
    public async Task Create_RejectsSecondOngoingSchedule()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), new NoopChatRealtimeNotifier());
        await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "First", null, new DateTimeOffset(now.AddDays(1)))), default);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateScheduleCommand(fixture.FreelancerId,
            new CreateScheduleRequest(fixture.ConversationId, "Second", null, new DateTimeOffset(now.AddDays(2)))), default));
    }

    [Fact]
    public async Task Create_CompletesElapsedScheduleBeforeCreatingNextSchedule()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        db.Schedules.Add(new Schedule
        {
            ScheduleId = Guid.NewGuid(),
            ConversationId = fixture.ConversationId,
            CreatedByUserId = fixture.ClientId,
            Title = "Elapsed",
            ScheduledAtUtc = now.AddMinutes(-1),
            Status = ScheduleStatus.Scheduled,
            CreatedAt = now.AddDays(-1)
        });
        await db.SaveChangesAsync();
        var handler = new ScheduleWorkflowHandlers(db, new FixedClock(now), new NoopChatRealtimeNotifier());

        await handler.Handle(new CreateScheduleCommand(fixture.FreelancerId,
            new CreateScheduleRequest(fixture.ConversationId, "Next", null, new DateTimeOffset(now.AddDays(1)))), default);

        var schedules = await db.Schedules.OrderBy(x => x.ScheduledAtUtc).ToListAsync();
        Assert.Equal(ScheduleStatus.Completed, schedules[0].Status);
        Assert.Equal(ScheduleStatus.Scheduled, schedules[1].Status);
    }

    [Fact]
    public void Model_HasUniqueScheduledScheduleIndexPerConversation()
    {
        using var db = CreateContext();

        var index = db.Model.FindEntityType(typeof(Schedule))!.GetIndexes()
            .Single(x => x.Name == "UX_Schedules_ConversationId_Scheduled");

        Assert.True(index.IsUnique);
        Assert.Equal("\"Status\" = 0", index.GetFilter());
        Assert.Collection(index.Properties,
            property => Assert.Equal(nameof(Schedule.ConversationId), property.Name));
    }

    private static GigbridgeDbContext CreateContext() => new(new DbContextOptionsBuilder<GigbridgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (Guid ClientId, Guid FreelancerId, Guid ConversationId) Seed(GigbridgeDbContext db, DateTime now)
    {
        var client = new User { UserId = Guid.NewGuid(), FullName = "Client", Email = "client@test.local", Role = 0, IsActive = true };
        var freelancer = new User { UserId = Guid.NewGuid(), FullName = "Freelancer", Email = "freelancer@test.local", Role = 1, IsActive = true };
        var conversation = new Conversation { ConversationsId = Guid.NewGuid(), CreatedByUserId = client.UserId,
            ConversationType = (int)ConversationType.JobNegotiation, Status = (int)ConversationStatus.Active, CreatedAt = now };
        db.AddRange(client, freelancer, conversation);
        db.AddRange(new ConversationParticipant { ConversationParticipantId = Guid.NewGuid(), ConversationsId = conversation.ConversationsId,
            UserId = client.UserId, User = client, Conversations = conversation, ParticipantRole = 0, JoinedAt = now },
            new ConversationParticipant { ConversationParticipantId = Guid.NewGuid(), ConversationsId = conversation.ConversationsId,
            UserId = freelancer.UserId, User = freelancer, Conversations = conversation, ParticipantRole = 1, JoinedAt = now });
        db.SaveChanges();
        return (client.UserId, freelancer.UserId, conversation.ConversationsId);
    }

    private sealed class FixedClock(DateTime now) : IDateTimeService { public DateTime UtcNow { get; } = now; }
}
