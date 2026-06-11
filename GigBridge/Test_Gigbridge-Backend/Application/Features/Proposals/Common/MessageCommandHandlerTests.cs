using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Messages.Send.Commands;
using Application.Features.Chat.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class MessageCommandHandlerTests
{
    [Fact]
    public async Task SendMessage_DuplicateClientMessageIdReturnsExistingMessage()
    {
        var fixture = new MessageFixture();
        var existingMessageId = Guid.NewGuid();
        fixture.Messages.Add(new Message
        {
            MessagesId = existingMessageId,
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.ClientUserId,
            MessageType = (int)MessageType.Text,
            Content = "Already sent",
            ClientMessageId = "mobile-1",
            SentAt = fixture.Now
        });

        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.ClientUserId,
                new SendMessageRequest(fixture.ConversationId, "mobile-1", "Already sent", null, [])),
            CancellationToken.None);

        Assert.Equal(existingMessageId, response.MessageId);
        Assert.Single(fixture.Messages.Entities);
    }

    [Fact]
    public async Task SendMessage_UserOutsideConversationCannotSend()
    {
        var fixture = new MessageFixture();
        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new SendMessageCommand(
                    Guid.NewGuid(),
                    new SendMessageRequest(fixture.ConversationId, "outsider-1", "hello", null, [])),
                CancellationToken.None));
    }

    [Fact]
    public async Task SendMessage_CreatesMessageAndIncrementsUnreadForOtherParticipants()
    {
        var fixture = new MessageFixture();
        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.ClientUserId,
                new SendMessageRequest(fixture.ConversationId, "mobile-2", "hello", null, [])),
            CancellationToken.None);

        var message = Assert.Single(fixture.Messages.Entities);
        Assert.Equal(response.MessageId, message.MessagesId);
        Assert.Equal("hello", message.Content);
        Assert.Equal(fixture.Now, fixture.Conversation.LastMessageAt);
        Assert.Equal(message.MessagesId, fixture.Conversation.LastMessageId);
        Assert.Equal(0, fixture.ClientParticipant.UnreadCount);
        Assert.Equal(1, fixture.FreelancerParticipant.UnreadCount);
    }

    private sealed class MessageFixture
    {
        public MessageFixture()
        {
            Conversation = new Conversation
            {
                ConversationsId = ConversationId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = Guid.NewGuid(),
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };
            ClientParticipant = new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = ConversationId,
                UserId = ClientUserId,
                ParticipantRole = (int)ParticipantRole.Client,
                JoinedAt = Now
            };
            FreelancerParticipant = new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = ConversationId,
                UserId = FreelancerUserId,
                ParticipantRole = (int)ParticipantRole.Freelancer,
                JoinedAt = Now
            };

            Context.AddSet(Conversation);
            Context.AddSet(ClientParticipant, FreelancerParticipant);
            Messages = Context.AddSet<Message>();
            Context.AddSet<MessageAttachment>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 9, 0, 0, DateTimeKind.Utc);
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Conversation Conversation { get; }
        public ConversationParticipant ClientParticipant { get; }
        public ConversationParticipant FreelancerParticipant { get; }
        public TestDbSet<Message> Messages { get; }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
