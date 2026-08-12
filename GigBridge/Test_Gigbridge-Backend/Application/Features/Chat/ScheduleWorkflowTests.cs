using Application.Features.Chat.Common.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Chat.Common.Messages;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Infrastructure.Persistence;
using Infrastructure.Services.Email;
using Test_Gigbridge_Backend.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Test_Gigbridge_Backend.Application.Features.Chat;

public class ScheduleWorkflowTests
{
    private static readonly ScheduleEmailRenderer EmailRenderer =
        new(TestTemplateReader.FromProjectTemplates());

    [Fact]
    public async Task Create_PersistsNeutralPermissionsAndPersonalizesRealtimePayloads()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), notifier, new NoopGoogleMeetOAuthService(), EmailRenderer);

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
        Assert.True(creatorMessage.Schedule.CanCancel);
        Assert.False(freelancerMessage.Schedule.CanCancel);
        Assert.True(freelancerMessage.Schedule.CanAccept);

        var creatorChanged = Assert.IsType<ScheduleEventResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.ClientId && x.EventName == "ScheduleChanged").Payload);
        var freelancerChanged = Assert.IsType<ScheduleEventResponse>(notifier.UserEvents.Single(x =>
            x.UserId == fixture.FreelancerId && x.EventName == "ScheduleChanged").Payload);
        Assert.True(creatorChanged.CanEdit);
        Assert.False(freelancerChanged.CanEdit);
        Assert.True(created.Message.Schedule!.CanEdit);

        Assert.True(MessageHelpers.ParseScheduleMetadata(persistedMessage, fixture.ClientId, now)!.CanEdit);
        Assert.False(MessageHelpers.ParseScheduleMetadata(persistedMessage, fixture.FreelancerId, now)!.CanEdit);

        var emailJobs = await db.DeliveryOutboxes.Where(x => x.Channel == (int)DeliveryChannel.Email).ToListAsync();
        Assert.Equal(2, emailJobs.Count);
        var realtimeJobs = await db.DeliveryOutboxes.Where(x => x.Channel == (int)DeliveryChannel.NotificationRealtime).ToListAsync();
        Assert.Equal(2, realtimeJobs.Count);
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var clientEmail = System.Text.Json.JsonSerializer.Deserialize<ScheduleDeliveryPayload>(
            emailJobs.Single(x => x.RecipientUserId == fixture.ClientId).Payload, json)!;
        var freelancerEmail = System.Text.Json.JsonSerializer.Deserialize<ScheduleDeliveryPayload>(
            emailJobs.Single(x => x.RecipientUserId == fixture.FreelancerId).Payload, json)!;
        Assert.StartsWith("Schedule proposal sent:", clientEmail.Subject);
        Assert.StartsWith("New schedule needs your response:", freelancerEmail.Subject);
        Assert.Contains("Hello Client", clientEmail.HtmlBody);
        Assert.Contains("Hello Freelancer", freelancerEmail.HtmlBody);
        Assert.Contains($"/messages?conversationId={fixture.ConversationId:D}&amp;messageId={persistedMessage.MessagesId:D}",
            freelancerEmail.HtmlBody);
        Assert.Contains("View schedule:", freelancerEmail.TextBody);
    }

    [Fact]
    public async Task Create_WithEmailDisabled_DoesNotQueueEmailOutboxRows()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), notifier,
            new NoopGoogleMeetOAuthService(), EmailRenderer);

        await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddDays(3)), SendEmailNotification: false)), default);

        var emailJobs = await db.DeliveryOutboxes.Where(x => x.Channel == (int)DeliveryChannel.Email).ToListAsync();
        Assert.Empty(emailJobs);

        var realtimeJobs = await db.DeliveryOutboxes.Where(x => x.Channel == (int)DeliveryChannel.NotificationRealtime).ToListAsync();
        Assert.Equal(2, realtimeJobs.Count);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Contains(notifier.UserEvents, x => x.UserId == fixture.ClientId && x.EventName == "ReceiveMessage");
        Assert.Contains(notifier.UserEvents, x => x.UserId == fixture.FreelancerId && x.EventName == "ReceiveMessage");
    }

    [Fact]
    public async Task CreatorGrace_AllowsSameDayCancellation_WithoutConsumingEditQuota()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc); // 13:00 ICT
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
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
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var starts = new DateTimeOffset(now.AddDays(2));
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review call", "Line one\r\nLine two", starts)), default);

        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new UpdateScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new UpdateScheduleRequest(" Review   call ", "Line one\nLine two", starts, accepted.Schedule.Version)), default));
        Assert.Equal(0, (await db.Schedules.SingleAsync()).EditCount);
    }

    [Fact]
    public async Task Edit_LongLeadSchedule_IsRejectedInsideFinal24Hours()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var starts = new DateTimeOffset(now.AddHours(24).AddMinutes(5));
        var createHandler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await createHandler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, starts)), default);

        var editHandler = new ScheduleWorkflowService(db, new FixedClock(now.AddMinutes(6)), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        await Assert.ThrowsAsync<BadRequestException>(() => editHandler.Handle(
            new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
                new UpdateScheduleRequest("Changed", null, starts, created.Schedule.Version)), default));
    }

    [Fact]
    public async Task Edit_ShortLeadSchedule_AllowsCreatorGraceInsideFinal24Hours()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var starts = new DateTimeOffset(now.AddHours(12));
        var createHandler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await createHandler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, starts)), default);

        var editHandler = new ScheduleWorkflowService(db, new FixedClock(now.AddMinutes(5)), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var edited = await editHandler.Handle(new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new UpdateScheduleRequest("Changed", null, starts, created.Schedule.Version)), default);

        Assert.Equal("Changed", edited.Schedule.Title);
    }

    [Fact]
    public async Task ClientEditQuota_RejectsThirdEdit()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var one = await handler.Handle(new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new UpdateScheduleRequest("Review one", null, new DateTimeOffset(now.AddDays(3)), accepted.Schedule.Version)), default);
        var two = await handler.Handle(new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
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
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "First", null, new DateTimeOffset(now.AddDays(1)))), default);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateScheduleCommand(fixture.ClientId,
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
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Next", null, new DateTimeOffset(now.AddDays(1)))), default);

        var schedules = await db.Schedules.OrderBy(x => x.ScheduledAtUtc).ToListAsync();
        Assert.Equal(ScheduleStatus.Completed, schedules[0].Status);
        Assert.Equal(ScheduleStatus.Scheduled, schedules[1].Status);
    }

    [Fact]
    public async Task AgreementFlow_RejectCounterProposeAndAccept_UsesExpectedRolesAndPermissions()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), notifier, new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        Assert.Equal((int)ScheduleAgreementStatus.AwaitingFreelancer, created.Schedule.AgreementStatus);
        Assert.False(created.Schedule.CanAccept);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new AcceptScheduleCommand(
            fixture.ClientId, created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default));

        var rejected = await handler.Handle(new RejectScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        Assert.Equal((int)ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal,
            rejected.Schedule.AgreementStatus);
        Assert.True(rejected.Schedule.CanProposeTime);
        Assert.Contains(await db.Notifications.ToListAsync(), n =>
            n.UserId == fixture.ClientId && n.Title == "Freelancer rejected the schedule");

        var counter = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(4)), rejected.Schedule.Version)), default);
        Assert.Equal((int)ScheduleAgreementStatus.AwaitingClient, counter.Schedule.AgreementStatus);
        Assert.True(counter.Schedule.CanEditCounterProposal);

        var clientView = await handler.Handle(new GetScheduleQuery(fixture.ClientId, created.Schedule.ScheduleId), default);
        Assert.True(clientView.CanAccept);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(counter.Schedule.Version)), default);
        Assert.Equal((int)ScheduleAgreementStatus.Accepted, accepted.Schedule.AgreementStatus);
        var scheduleCard = await db.Messages.SingleAsync(x => x.ScheduleId == created.Schedule.ScheduleId);
        Assert.Equal(ScheduleEventType.Accepted, scheduleCard.ScheduleEventType);
        Assert.Equal(accepted.Schedule.Version, scheduleCard.ScheduleEventSequence);
        var startDeliveries = await db.DeliveryOutboxes
            .Where(x => x.ScheduleId == created.Schedule.ScheduleId && x.DeliveryKey.Contains(":start:"))
            .ToListAsync();
        Assert.Equal(4, startDeliveries.Count);
        Assert.All(startDeliveries, delivery =>
        {
            Assert.Equal((int)DeliveryOutboxStatus.Pending, delivery.Status);
            Assert.Equal(accepted.Schedule.ScheduledAtUtc, delivery.NextAttemptAt);
            var payload = System.Text.Json.JsonSerializer.Deserialize<ScheduleDeliveryPayload>(delivery.Payload,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Assert.NotNull(payload);
            Assert.True(payload.CreateNotificationAtDelivery);
            Assert.Equal("Meeting time reached", payload.NotificationTitle);
            Assert.Contains("Your meeting starts now", payload.HtmlBody);
            Assert.Contains("GigBridge", payload.HtmlBody);
            Assert.False(string.IsNullOrWhiteSpace(payload.TextBody));
        });
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new AcceptScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(counter.Schedule.Version)), default));
    }

    [Fact]
    public void MeetingStartEmail_IsBrandedAndHtmlEncodesUserContent()
    {
        var email = EmailRenderer.Render(ScheduleNotificationType.MeetingStarting,
            new ScheduleEmailModel("Taylor", "GigBridge", false, "Review <script>",
                "23 Jun 2026, 13:30 ICT", "Discuss & confirm", null,
                "https://app.gigbridge.test/messages?conversationId=1",
                "https://meet.google.com/abc-defg-hij"));

        Assert.Contains("<!doctype html>", email.HtmlBody);
        Assert.Contains("Review &lt;script&gt;", email.HtmlBody);
        Assert.Contains("Discuss &amp; confirm", email.HtmlBody);
        Assert.Contains("Join meeting", email.HtmlBody);
        Assert.Contains("Join meeting: https://meet.google.com/abc-defg-hij", email.TextBody);
        Assert.DoesNotContain("Review <script>", email.HtmlBody);
    }

    [Theory]
    [InlineData(ScheduleNotificationType.ProposalCreated, "Schedule proposal sent", "Your schedule proposal was sent")]
    [InlineData(ScheduleNotificationType.ScheduleUpdated, "Schedule updated", "Your schedule changes were saved")]
    [InlineData(ScheduleNotificationType.ScheduleDeclined, "You declined the schedule", "You declined the proposed schedule")]
    [InlineData(ScheduleNotificationType.CounterProposalCreated, "New time proposed", "Your counterproposal was sent")]
    [InlineData(ScheduleNotificationType.CounterProposalUpdated, "Proposed time updated", "Your proposed time was updated")]
    [InlineData(ScheduleNotificationType.ScheduleConfirmed, "Schedule confirmed", "You confirmed the schedule")]
    [InlineData(ScheduleNotificationType.CounterProposalDeclined, "You declined the proposed time", "You declined the proposed time")]
    [InlineData(ScheduleNotificationType.ScheduleCancelled, "Schedule cancelled", "You cancelled the schedule")]
    [InlineData(ScheduleNotificationType.MeetingStarting, "Meeting starts now", "Your meeting starts now")]
    public void ScheduleNotificationTypes_RenderMappedSubjectHeadlineAndFallback(
        ScheduleNotificationType type, string expectedSubject, string expectedHeadline)
    {
        var email = EmailRenderer.Render(type,
            new ScheduleEmailModel("Taylor", "Taylor", true, "Design review", "23 Jun 2026, 13:30 ICT",
                "Review the final design", "Client unavailable", "https://app.gigbridge.test/messages?conversationId=1"));

        Assert.Contains(expectedSubject, email.Subject);
        Assert.Contains(expectedHeadline, email.HtmlBody);
        Assert.Contains("Hello Taylor", email.HtmlBody);
        Assert.Contains("View schedule", email.HtmlBody);
        Assert.Contains("Client unavailable", email.TextBody);
    }

    [Fact]
    public async Task AcceptedScheduleEditReschedulesAndCancellationCancelsStartDeliveries()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var startDeliveries = await db.DeliveryOutboxes
            .Where(x => x.ScheduleId == created.Schedule.ScheduleId && x.DeliveryKey.Contains(":start:"))
            .ToListAsync();
        foreach (var delivery in startDeliveries)
        {
            delivery.Status = (int)DeliveryOutboxStatus.Processing;
            delivery.ClaimToken = Guid.NewGuid();
        }
        await db.SaveChangesAsync();

        var newStart = now.AddDays(4);
        var edited = await handler.Handle(new UpdateScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new UpdateScheduleRequest("Review", null, new DateTimeOffset(newStart), accepted.Schedule.Version)), default);

        Assert.Equal(4, startDeliveries.Count);
        Assert.All(startDeliveries, delivery =>
        {
            Assert.Equal((int)DeliveryOutboxStatus.Pending, delivery.Status);
            Assert.Equal(newStart, delivery.NextAttemptAt);
            Assert.Null(delivery.ClaimToken);
        });

        foreach (var delivery in startDeliveries)
        {
            delivery.Status = (int)DeliveryOutboxStatus.Processing;
            delivery.ClaimToken = Guid.NewGuid();
        }
        await db.SaveChangesAsync();

        await handler.Handle(new CancelScheduleCommand(fixture.ClientId, created.Schedule.ScheduleId,
            new CancelScheduleRequest("No longer needed", edited.Schedule.Version)), default);
        Assert.All(startDeliveries, delivery =>
        {
            Assert.Equal((int)DeliveryOutboxStatus.Cancelled, delivery.Status);
            Assert.Null(delivery.ClaimToken);
        });
    }

    [Fact]
    public async Task CounterProposalEdit_ClosesExactlyTwentyFourHoursAfterCreation()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(4)))), default);
        var rejected = await handler.Handle(new RejectScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var counter = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(5)), rejected.Schedule.Version)), default);

        var beforeExpiryHandler = new ScheduleWorkflowService(db, new FixedClock(now.AddHours(23).AddMinutes(59)),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var edited = await beforeExpiryHandler.Handle(new UpdateCounterProposalCommand(
            fixture.FreelancerId, created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(6)), counter.Schedule.Version)), default);

        var expiredHandler = new ScheduleWorkflowService(db, new FixedClock(now.AddHours(24)),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        await Assert.ThrowsAsync<BadRequestException>(() => expiredHandler.Handle(new UpdateCounterProposalCommand(
            fixture.FreelancerId, created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(7)), edited.Schedule.Version)), default));
    }

    [Fact]
    public async Task CounterProposalEdit_ClosesWhenProposedTimeArrives()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        var rejected = await handler.Handle(new RejectScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var counter = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddHours(2)), rejected.Schedule.Version)), default);

        var startHandler = new ScheduleWorkflowService(db, new FixedClock(now.AddHours(2)),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        await Assert.ThrowsAsync<BadRequestException>(() => startHandler.Handle(new UpdateCounterProposalCommand(
            fixture.FreelancerId, created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(1)), counter.Schedule.Version)), default));
    }

    [Fact]
    public async Task ClientRejectingCounterProposal_IsTerminalAndAllowsNewSchedule()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now), new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null, new DateTimeOffset(now.AddDays(3)))), default);
        var rejected = await handler.Handle(new RejectScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var counter = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(4)), rejected.Schedule.Version)), default);
        var terminal = await handler.Handle(new RejectScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(counter.Schedule.Version)), default);

        Assert.Equal((int)ScheduleStatus.Rejected, terminal.Schedule.Status);
        var next = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Next", null, new DateTimeOffset(now.AddDays(5)))), default);
        Assert.Equal((int)ScheduleAgreementStatus.AwaitingFreelancer, next.Schedule.AgreementStatus);
    }

    [Fact]
    public async Task AcceptedSchedule_FreelancerCanRequestThreeDateChanges_ClientCanRejectOrAccept()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var originalTime = accepted.Schedule.ScheduledAtUtc;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new UpdateScheduleCommand(fixture.FreelancerId, created.Schedule.ScheduleId,
                new UpdateScheduleRequest("Direct edit", null, new DateTimeOffset(now.AddDays(4)),
                    accepted.Schedule.Version)), default));

        var rescheduleEventTime = now.AddMinutes(1);
        handler = new ScheduleWorkflowService(db, new FixedClock(rescheduleEventTime),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);
        var version = accepted.Schedule.Version;
        for (var requestNumber = 1; requestNumber <= 2; requestNumber++)
        {
            var requestedTime = now.AddDays(3 + requestNumber);
            var requested = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
                created.Schedule.ScheduleId,
                new CounterProposalRequest(new DateTimeOffset(requestedTime), version)), default);

            Assert.Equal((int)ScheduleAgreementStatus.AwaitingClientReschedule,
                requested.Schedule.AgreementStatus);
            Assert.Equal(originalTime, requested.Schedule.ScheduledAtUtc);
            Assert.Equal(requestedTime, requested.Schedule.ProposedScheduledAtUtc);
            Assert.Equal(requestNumber, requested.Schedule.RescheduleRequestCount);
            Assert.Equal(3 - requestNumber, requested.Schedule.RemainingRescheduleRequests);
            if (requestNumber == 1)
            {
                var movedCard = await db.Messages.SingleAsync(
                    message => message.ScheduleId == created.Schedule.ScheduleId);
                Assert.Equal(rescheduleEventTime, movedCard.SentAt);
                Assert.Equal(fixture.FreelancerId, movedCard.SenderUserId);
            }

            var rejected = await handler.Handle(new RejectScheduleCommand(fixture.ClientId,
                created.Schedule.ScheduleId, new ScheduleVersionRequest(requested.Schedule.Version)), default);
            Assert.Equal((int)ScheduleAgreementStatus.RescheduleRejected, rejected.Schedule.AgreementStatus);
            Assert.Equal(ScheduleStatus.Scheduled, (ScheduleStatus)rejected.Schedule.Status);
            Assert.Equal(originalTime, rejected.Schedule.ScheduledAtUtc);
            Assert.Null(rejected.Schedule.ProposedScheduledAtUtc);
            version = rejected.Schedule.Version;
        }

        var finalRequestedTime = now.AddDays(7);
        var finalRequest = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(finalRequestedTime), version)), default);
        var finalAccepted = await handler.Handle(new AcceptScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(finalRequest.Schedule.Version)), default);

        Assert.Equal((int)ScheduleAgreementStatus.Accepted, finalAccepted.Schedule.AgreementStatus);
        Assert.Equal(finalRequestedTime, finalAccepted.Schedule.ScheduledAtUtc);
        Assert.Null(finalAccepted.Schedule.ProposedScheduledAtUtc);
        Assert.Equal(3, finalAccepted.Schedule.RescheduleRequestCount);
        Assert.Equal(0, finalAccepted.Schedule.RemainingRescheduleRequests);

        var freelancerView = await handler.Handle(new GetScheduleQuery(
            fixture.FreelancerId, created.Schedule.ScheduleId), default);
        Assert.False(freelancerView.CanProposeTime);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateCounterProposalCommand(fixture.FreelancerId, created.Schedule.ScheduleId,
                new CounterProposalRequest(new DateTimeOffset(now.AddDays(8)),
                    finalAccepted.Schedule.Version)), default));
    }

    [Fact]
    public async Task FutureCounterProposal_RemainsActionableAfterOriginalTimePasses()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddHours(1)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var requested = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddHours(4)),
                accepted.Schedule.Version)), default);

        var responseTime = now.AddHours(2);
        handler = new ScheduleWorkflowService(db, new FixedClock(responseTime),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var clientView = await handler.Handle(new GetScheduleQuery(
            fixture.ClientId, created.Schedule.ScheduleId), default);
        var freelancerView = await handler.Handle(new GetScheduleQuery(
            fixture.FreelancerId, created.Schedule.ScheduleId), default);
        Assert.True(clientView.CanAccept);
        Assert.True(clientView.CanReject);
        Assert.True(freelancerView.CanEditCounterProposal);

        var scheduleMessage = await db.Messages.SingleAsync(
            message => message.ScheduleId == created.Schedule.ScheduleId);
        Assert.True(MessageHelpers.ParseScheduleMetadata(
            scheduleMessage, fixture.ClientId, responseTime)!.CanAccept);
        Assert.True(MessageHelpers.ParseScheduleMetadata(
            scheduleMessage, fixture.ClientId, responseTime)!.CanReject);
        Assert.True(MessageHelpers.ParseScheduleMetadata(
            scheduleMessage, fixture.FreelancerId, responseTime)!.CanEditCounterProposal);

        var edited = await handler.Handle(new UpdateCounterProposalCommand(
            fixture.FreelancerId, created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddHours(5)),
                requested.Schedule.Version)), default);
        var finalAccepted = await handler.Handle(new AcceptScheduleCommand(
            fixture.ClientId, created.Schedule.ScheduleId,
            new ScheduleVersionRequest(edited.Schedule.Version)), default);

        Assert.Equal(now.AddHours(5), finalAccepted.Schedule.ScheduledAtUtc);
        Assert.Equal((int)ScheduleAgreementStatus.Accepted,
            finalAccepted.Schedule.AgreementStatus);
    }

    [Fact]
    public async Task AcceptedSchedule_FreelancerCanCancelWhileDateChangeIsAwaitingClient()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);
        var requested = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(4)),
                accepted.Schedule.Version)), default);

        var freelancerView = await handler.Handle(new GetScheduleQuery(
            fixture.FreelancerId, created.Schedule.ScheduleId), default);
        Assert.True(freelancerView.CanCancel);

        const string reason = "I am no longer available";
        var cancelled = await handler.Handle(new CancelScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CancelScheduleRequest(reason, requested.Schedule.Version)), default);

        Assert.Equal((int)ScheduleStatus.Cancelled, cancelled.Schedule.Status);
        Assert.Equal(fixture.FreelancerId, cancelled.Schedule.CancelledByUserId);
        Assert.Equal(reason, cancelled.Schedule.CancellationReason);
        Assert.Null(cancelled.Schedule.ProposedScheduledAtUtc);
        Assert.Equal((int)ScheduleEventType.Cancelled,
            cancelled.Message.Schedule!.EventType);
    }

    [Fact]
    public async Task ThirdRejectedFreelancerDateChange_AutomaticallyCancelsMeeting()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);

        var version = accepted.Schedule.Version;
        ScheduleMutationResult? rejected = null;
        for (var requestNumber = 1; requestNumber <= 3; requestNumber++)
        {
            var requested = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
                created.Schedule.ScheduleId,
                new CounterProposalRequest(new DateTimeOffset(now.AddDays(3 + requestNumber)),
                    version)), default);
            rejected = await handler.Handle(new RejectScheduleCommand(fixture.ClientId,
                created.Schedule.ScheduleId,
                new ScheduleVersionRequest(requested.Schedule.Version)), default);
            version = rejected.Schedule.Version;

            if (requestNumber < 3)
                Assert.Equal((int)ScheduleStatus.Scheduled, rejected.Schedule.Status);
        }

        Assert.NotNull(rejected);
        Assert.Equal((int)ScheduleStatus.Cancelled, rejected.Schedule.Status);
        Assert.Equal((int)ScheduleAgreementStatus.RescheduleRejected,
            rejected.Schedule.AgreementStatus);
        Assert.Equal(3, rejected.Schedule.RescheduleRequestCount);
        Assert.Equal(0, rejected.Schedule.RemainingRescheduleRequests);
        Assert.Null(rejected.Schedule.CancelledByUserId);
        Assert.Null(rejected.Schedule.ProposedScheduledAtUtc);
        Assert.Equal(
            "Automatically cancelled after the client rejected three freelancer reschedule requests.",
            rejected.Schedule.CancellationReason);
        Assert.Equal((int)ScheduleEventType.Cancelled,
            rejected.Message.Schedule!.EventType);

        var ongoing = await handler.Handle(new GetOngoingScheduleQuery(
            fixture.FreelancerId, fixture.ConversationId), default);
        Assert.False(ongoing.HasOngoingSchedule);
        Assert.Null(ongoing.ScheduleId);
    }

    [Fact]
    public async Task ThirdDateChangeWithOnlyTwoRejections_DoesNotCancelMeeting()
    {
        var now = new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        var fixture = Seed(db, now);
        var handler = new ScheduleWorkflowService(db, new FixedClock(now),
            new NoopChatRealtimeNotifier(), new NoopGoogleMeetOAuthService(), EmailRenderer);

        var created = await handler.Handle(new CreateScheduleCommand(fixture.ClientId,
            new CreateScheduleRequest(fixture.ConversationId, "Review", null,
                new DateTimeOffset(now.AddDays(3)))), default);
        var accepted = await handler.Handle(new AcceptScheduleCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId, new ScheduleVersionRequest(created.Schedule.Version)), default);

        var firstRequest = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
            created.Schedule.ScheduleId,
            new CounterProposalRequest(new DateTimeOffset(now.AddDays(4)),
                accepted.Schedule.Version)), default);
        var firstAccepted = await handler.Handle(new AcceptScheduleCommand(fixture.ClientId,
            created.Schedule.ScheduleId,
            new ScheduleVersionRequest(firstRequest.Schedule.Version)), default);

        var version = firstAccepted.Schedule.Version;
        ScheduleMutationResult? lastRejection = null;
        for (var requestNumber = 2; requestNumber <= 3; requestNumber++)
        {
            var requested = await handler.Handle(new CreateCounterProposalCommand(fixture.FreelancerId,
                created.Schedule.ScheduleId,
                new CounterProposalRequest(new DateTimeOffset(now.AddDays(3 + requestNumber)),
                    version)), default);
            lastRejection = await handler.Handle(new RejectScheduleCommand(fixture.ClientId,
                created.Schedule.ScheduleId,
                new ScheduleVersionRequest(requested.Schedule.Version)), default);
            version = lastRejection.Schedule.Version;
        }

        Assert.NotNull(lastRejection);
        Assert.Equal((int)ScheduleStatus.Scheduled, lastRejection.Schedule.Status);
        Assert.Equal((int)ScheduleAgreementStatus.RescheduleRejected,
            lastRejection.Schedule.AgreementStatus);
        Assert.Equal(3, lastRejection.Schedule.RescheduleRequestCount);
        Assert.Null(lastRejection.Schedule.CancellationReason);
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

    [Fact]
    public void Model_HasPartialActiveDeliveryIndexAlignedWithClaimOrdering()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(DeliveryOutbox))!;
        var index = entity.GetIndexes()
            .Single(x => x.Name == "IX_DeliveryOutboxes_Active_Channel_Status_Due_Id");

        Assert.Equal("\"Status\" IN (0, 1)", index.GetFilter());
        Assert.Collection(index.Properties,
            property => Assert.Equal(nameof(DeliveryOutbox.Channel), property.Name),
            property => Assert.Equal(nameof(DeliveryOutbox.Status), property.Name),
            property => Assert.Equal(nameof(DeliveryOutbox.NextAttemptAt), property.Name),
            property => Assert.Equal(nameof(DeliveryOutbox.DeliveryOutboxId), property.Name));
        Assert.True(entity.FindProperty(nameof(DeliveryOutbox.ClaimToken))!.IsConcurrencyToken);
    }

    [Fact]
    public async Task ClaimToken_PreventsAStaleScheduleWriteFromOverwritingAWorkerClaim()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;
        var deliveryId = Guid.NewGuid();
        await using (var seed = new GigbridgeDbContext(options))
        {
            seed.DeliveryOutboxes.Add(new DeliveryOutbox
            {
                DeliveryOutboxId = deliveryId,
                DeliveryKey = $"test:{deliveryId}",
                RecipientUserId = Guid.NewGuid(),
                Payload = "{}",
                Status = (int)DeliveryOutboxStatus.Pending,
                NextAttemptAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var scheduleContext = new GigbridgeDbContext(options);
        var staleDelivery = await scheduleContext.DeliveryOutboxes.SingleAsync();
        await using (var workerContext = new GigbridgeDbContext(options))
        {
            var claimedDelivery = await workerContext.DeliveryOutboxes.SingleAsync();
            claimedDelivery.Status = (int)DeliveryOutboxStatus.Processing;
            claimedDelivery.ClaimToken = Guid.NewGuid();
            await workerContext.SaveChangesAsync();
        }

        staleDelivery.Status = (int)DeliveryOutboxStatus.Cancelled;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => scheduleContext.SaveChangesAsync());
    }

    private static GigbridgeDbContext CreateContext() => new(new DbContextOptionsBuilder<GigbridgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class NoopGoogleMeetOAuthService : IGoogleMeetOAuthService
    {
        public Task<AuthorizationUrlResult> GetAuthorizationUrlAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new AuthorizationUrlResult("https://example.com", DateTime.UtcNow.AddMinutes(5), Guid.NewGuid()));
        public Task<string> HandleCallbackAsync(Guid userId, string state, string? code, string? error, CancellationToken ct) =>
            Task.FromResult("success");
        public Task<GoogleMeetConnectionStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new GoogleMeetConnectionStatusResponse(false, null, null, false));
        public Task DisconnectAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken ct) => Task.FromResult<string?>(null);
    }

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
