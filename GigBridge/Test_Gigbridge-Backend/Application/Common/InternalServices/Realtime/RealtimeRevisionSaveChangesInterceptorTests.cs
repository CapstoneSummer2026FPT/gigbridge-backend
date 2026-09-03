using System.Text.Json;
using Application.Common.InternalServices.Realtime.Models;
using Application.Common.InternalServices.Realtime.Services;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Realtime;

public sealed class RealtimeRevisionSaveChangesInterceptorTests
{
    [Fact]
    public async Task NewConversation_UsesTrackedParticipantsAndBumpsEachUserOnce()
    {
        var databaseName = $"realtime-new-conversation-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var conversationId = Guid.NewGuid();
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        await using var context = CreateContext(databaseName, root, withInterceptor: true);
        context.Users.AddRange(
            new User { UserId = clientUserId, FullName = "Client", Email = "new-client@example.test" },
            new User { UserId = freelancerUserId, FullName = "Freelancer", Email = "new-freelancer@example.test" });
        context.Conversations.Add(new Conversation
        {
            ConversationsId = conversationId,
            ConversationType = (int)ConversationType.JobNegotiation,
            CreatedByUserId = clientUserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        context.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversationId,
                UserId = clientUserId,
                ParticipantRole = (int)ParticipantRole.Client,
                JoinedAt = DateTime.UtcNow
            },
            new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversationId,
                UserId = freelancerUserId,
                ParticipantRole = (int)ParticipantRole.Freelancer,
                JoinedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var states = await context.UserRealtimeStates.AsNoTracking().ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state => Assert.Equal(1, state.ConversationRevision));
        Assert.Equal(2, await context.DeliveryOutboxes.CountAsync(delivery =>
            delivery.DeliveryType == (int)DeliveryOutboxType.ConversationInboxRevision));
    }

    [Fact]
    public async Task NegotiationDraftChanges_BumpEachParticipantOnceAndQueueDurableEvents()
    {
        var fixture = await CreateFixture();
        await using var context = CreateContext(fixture.DatabaseName, fixture.Root, withInterceptor: true);

        context.NegotiationMilestoneDrafts.AddRange(
            NewDraft(fixture.ConversationId, "Discovery", 0),
            NewDraft(fixture.ConversationId, "Delivery", 1));
        await context.SaveChangesAsync();

        var states = await context.UserRealtimeStates.AsNoTracking().OrderBy(state => state.UserId).ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state =>
        {
            Assert.Equal(1, state.ConversationRevision);
            Assert.Equal(0, state.ConversationUnreadCount);
        });

        var deliveries = await context.DeliveryOutboxes.AsNoTracking()
            .Where(delivery => delivery.DeliveryType == (int)DeliveryOutboxType.ConversationInboxRevision)
            .ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, delivery =>
        {
            var payload = JsonSerializer.Deserialize<ConversationInboxRevisionChangedPayload>(
                delivery.Payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(payload);
            Assert.Equal(1, payload.Revision);
            Assert.Equal(fixture.ConversationId, payload.ConversationId);
        });
    }

    [Fact]
    public async Task ConversationAndUnreadChange_BumpOncePerParticipantAndPreserveUnreadDelta()
    {
        var fixture = await CreateFixture();
        await using var context = CreateContext(fixture.DatabaseName, fixture.Root, withInterceptor: true);
        var conversation = await context.Conversations.SingleAsync();
        var freelancer = await context.ConversationParticipants.SingleAsync(participant =>
            participant.UserId == fixture.FreelancerUserId);

        conversation.UpdatedAt = DateTime.UtcNow;
        freelancer.UnreadCount = 1;
        await context.SaveChangesAsync();

        var clientState = await context.UserRealtimeStates.SingleAsync(state => state.UserId == fixture.ClientUserId);
        var freelancerState = await context.UserRealtimeStates.SingleAsync(state => state.UserId == fixture.FreelancerUserId);
        Assert.Equal(1, clientState.ConversationRevision);
        Assert.Equal(0, clientState.ConversationUnreadCount);
        Assert.Equal(1, freelancerState.ConversationRevision);
        Assert.Equal(1, freelancerState.ConversationUnreadCount);
        Assert.Equal(2, await context.DeliveryOutboxes.CountAsync(delivery =>
            delivery.DeliveryType == (int)DeliveryOutboxType.ConversationInboxRevision));
    }

    [Fact]
    public async Task ConversationUnreadChange_RepairsDriftedRealtimeCountFromAuthoritativeParticipants()
    {
        var fixture = await CreateFixture();
        await using var context = CreateContext(fixture.DatabaseName, fixture.Root, withInterceptor: true);
        var conversation = await context.Conversations.SingleAsync();
        var freelancer = await context.ConversationParticipants.SingleAsync(participant =>
            participant.UserId == fixture.FreelancerUserId);

        conversation.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var freelancerState = await context.UserRealtimeStates.SingleAsync(state =>
            state.UserId == fixture.FreelancerUserId);

        freelancerState.ConversationUnreadCount = 3;
        await context.SaveChangesAsync();

        freelancer.UnreadCount = 1;
        await context.SaveChangesAsync();

        Assert.Equal(1, freelancerState.ConversationUnreadCount);
    }

    [Fact]
    public async Task OfferCreationAndEveryResponseStatus_BumpOncePerParticipantPerSave()
    {
        var fixture = await CreateFixture();
        await using var context = CreateContext(fixture.DatabaseName, fixture.Root, withInterceptor: true);
        var offer = new NegotiationOffer
        {
            NegotiationOfferId = Guid.NewGuid(),
            ConversationsId = fixture.ConversationId,
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            FreelancerProfilesId = Guid.NewGuid(),
            FinalPrice = 100,
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.NegotiationOffers.Add(offer);
        await context.SaveChangesAsync();

        foreach (var status in new[] { 3, 2, 1 })
        {
            offer.Status = status;
            offer.RespondedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var states = await context.UserRealtimeStates.AsNoTracking().ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state => Assert.Equal(4, state.ConversationRevision));
        Assert.Equal(8, await context.DeliveryOutboxes.CountAsync(delivery =>
            delivery.DeliveryType == (int)DeliveryOutboxType.ConversationInboxRevision));
    }

    private static async Task<Fixture> CreateFixture()
    {
        var databaseName = $"realtime-revision-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        await using var context = CreateContext(databaseName, root, withInterceptor: false);
        context.Users.AddRange(
            new User { UserId = clientUserId, FullName = "Client", Email = "client@example.test" },
            new User { UserId = freelancerUserId, FullName = "Freelancer", Email = "freelancer@example.test" });
        context.Conversations.Add(new Conversation
        {
            ConversationsId = conversationId,
            ConversationType = (int)ConversationType.JobNegotiation,
            CreatedByUserId = clientUserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        context.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversationId,
                UserId = clientUserId,
                ParticipantRole = (int)ParticipantRole.Client,
                JoinedAt = DateTime.UtcNow
            },
            new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversationId,
                UserId = freelancerUserId,
                ParticipantRole = (int)ParticipantRole.Freelancer,
                JoinedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        return new Fixture(databaseName, root, conversationId, clientUserId, freelancerUserId);
    }

    private static GigbridgeDbContext CreateContext(
        string databaseName,
        InMemoryDatabaseRoot root,
        bool withInterceptor)
    {
        var builder = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(databaseName, root);
        if (withInterceptor)
            builder.AddInterceptors(new RealtimeRevisionSaveChangesInterceptor());
        return new GigbridgeDbContext(builder.Options);
    }

    private static NegotiationMilestoneDraft NewDraft(Guid conversationId, string title, int orderIndex) => new()
    {
        NegotiationMilestoneDraftId = Guid.NewGuid(),
        ConversationsId = conversationId,
        Title = title,
        Amount = 100,
        Deliverables = title,
        AcceptanceCriteria = title,
        OrderIndex = orderIndex,
        CreatedAt = DateTime.UtcNow
    };

    private sealed record Fixture(
        string DatabaseName,
        InMemoryDatabaseRoot Root,
        Guid ConversationId,
        Guid ClientUserId,
        Guid FreelancerUserId);
}
