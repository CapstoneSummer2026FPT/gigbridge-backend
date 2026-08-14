using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;
using Application.Features.Chat.Common.Messages.GetAround;
using Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Disputes;
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
        fixture.Attachments.Add(new MessageAttachment
        {
            MessageAttachmentsId = Guid.NewGuid(),
            MessagesId = existingMessageId,
            FileName = "existing.pdf",
            FileUrl = "https://files.example/existing.pdf",
            StorageProvider = "cloudinary",
            MimeType = "application/pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 128,
            CreatedAt = fixture.Now
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
        Assert.Equal("mobile-1", response.ClientMessageId);
        var attachment = Assert.Single(response.Attachments);
        Assert.Equal("existing.pdf", attachment.FileName);
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
    public async Task SendMessage_DisputedWorkspaceConversationIsReadOnly()
    {
        var fixture = new MessageFixture();
        var contractId = Guid.NewGuid();
        fixture.Conversation.ConversationType = (int)ConversationType.ContractWorkroom;
        fixture.Conversation.ContractsId = contractId;
        fixture.Context.AddSet(new Contract
        {
            ContractsId = contractId,
            Title = "Disputed contract",
            Status = (int)ContractStatus.Disputed,
            CreatedAt = fixture.Now
        });

        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SendMessageCommand(
                    fixture.ClientUserId,
                    new SendMessageRequest(
                        fixture.ConversationId,
                        "disputed-workspace-1",
                        "This must not be sent.",
                        null,
                        [])),
                CancellationToken.None));

        Assert.Equal(
            "This contract is currently under dispute. Please continue communication in the dispute conversation.",
            exception.Message);
        Assert.Empty(fixture.Messages.Entities);
        Assert.Null(fixture.Conversation.LastMessageId);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    [Fact]
    public async Task SendMessage_ClosedDisputeIsRejectedEvenIfConversationStatusIsStillActive()
    {
        // Regression coverage for the "procedural sync" fragility: Conversation.Status is
        // normally closed in lockstep with Dispute.Status by UpdateAdminDisputeStatusCommandHandler,
        // but the send handler must also check Dispute.Status directly, so a send can never
        // slip through even if that sync were ever missed (Conversation.Status left Active here).
        var fixture = new MessageFixture();
        var disputeId = Guid.NewGuid();
        fixture.Conversation.ConversationType = (int)ConversationType.Dispute;
        fixture.Conversation.DisputesId = disputeId;
        fixture.Context.AddSet(new Dispute
        {
            DisputesId = disputeId,
            ContractsId = Guid.NewGuid(),
            InitiatorId = fixture.ClientUserId,
            Reason = "Payment dispute",
            Status = (int)DisputeStatus.Closed,
            CreatedAt = fixture.Now
        });

        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SendMessageCommand(
                    fixture.ClientUserId,
                    new SendMessageRequest(fixture.ConversationId, "closed-dispute-1", "Please refund me.", null, [])),
                CancellationToken.None));

        Assert.Equal("This dispute is closed and no longer accepts new messages.", exception.Message);
        Assert.Empty(fixture.Messages.Entities);
    }

    [Fact]
    public async Task SendMessage_OpenDisputeConversationStillAcceptsMessages()
    {
        var fixture = new MessageFixture();
        var disputeId = Guid.NewGuid();
        fixture.Conversation.ConversationType = (int)ConversationType.Dispute;
        fixture.Conversation.DisputesId = disputeId;
        fixture.Context.AddSet(new Dispute
        {
            DisputesId = disputeId,
            ContractsId = Guid.NewGuid(),
            InitiatorId = fixture.ClientUserId,
            Reason = "Payment dispute",
            Status = (int)DisputeStatus.InProgress,
            CreatedAt = fixture.Now
        });

        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.ClientUserId,
                new SendMessageRequest(fixture.ConversationId, "open-dispute-1", "Here is my evidence.", null, [])),
            CancellationToken.None);

        Assert.Equal("Here is my evidence.", response.Content);
        Assert.Single(fixture.Messages.Entities);
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

    [Fact]
    public async Task SendMessage_FromFreelancerBroadcastsToClientAndFreelancerUsers()
    {
        var fixture = new MessageFixture();
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            notifier);

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.FreelancerUserId,
                new SendMessageRequest(
                    fixture.ConversationId,
                    "freelancer-realtime-1",
                    "Message from freelancer",
                    null,
                    [])),
            CancellationToken.None);

        var receiveEvent = Assert.Single(
            notifier.UsersEvents,
            realtimeEvent => realtimeEvent.EventName == "ReceiveMessage");
        Assert.Contains(fixture.ClientUserId, receiveEvent.UserIds);
        Assert.Contains(fixture.FreelancerUserId, receiveEvent.UserIds);
        Assert.Equal(fixture.FreelancerUserId, response.SenderUserId);
        Assert.Equal(1, fixture.ClientParticipant.UnreadCount);
        Assert.Equal(0, fixture.FreelancerParticipant.UnreadCount);
    }

    [Fact]
    public async Task SendMessage_TextOnlyMessageAllowsMissingAttachments()
    {
        var fixture = new MessageFixture();
        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.ClientUserId,
                new SendMessageRequest(fixture.ConversationId, "mobile-no-attachments", "hello", null)),
            CancellationToken.None);

        var message = Assert.Single(fixture.Messages.Entities);
        Assert.Equal(response.MessageId, message.MessagesId);
        Assert.Equal("hello", message.Content);
        Assert.Empty(response.Attachments);
    }

    [Fact]
    public async Task SendMessage_ReturnsAttachmentPayloadAndBroadcastsToParticipantUsers()
    {
        var fixture = new MessageFixture();
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            notifier);

        var response = await handler.Handle(
            new SendMessageCommand(
                fixture.ClientUserId,
                new SendMessageRequest(
                    fixture.ConversationId,
                    "mobile-attachments",
                    "see file",
                    null,
                    [
                        new SendMessageAttachmentRequest(
                            "brief.pdf",
                            "https://files.example/brief.pdf",
                            "cloudinary",
                            "chat/brief",
                            "application/pdf",
                            ".pdf",
                            2048)
                    ])),
            CancellationToken.None);

        Assert.Equal("mobile-attachments", response.ClientMessageId);
        Assert.Null(response.EditedAt);
        var attachment = Assert.Single(response.Attachments);
        Assert.Equal("brief.pdf", attachment.FileName);
        Assert.Equal("https://files.example/brief.pdf", attachment.FileUrl);

        var receiveEvent = Assert.Single(
            notifier.UsersEvents,
            realtimeEvent => realtimeEvent.EventName == "ReceiveMessage");
        Assert.Contains(fixture.ClientUserId, receiveEvent.UserIds);
        Assert.Contains(fixture.FreelancerUserId, receiveEvent.UserIds);

        var conversationUpdates = notifier.UserEvents
            .Where(realtimeEvent => realtimeEvent.EventName == "ConversationUpdated")
            .ToList();
        Assert.Equal(2, conversationUpdates.Count);
    }

    [Fact]
    public async Task SendMessage_ReplyTargetMustBelongToSameConversation()
    {
        var fixture = new MessageFixture();
        var replyTargetId = Guid.NewGuid();
        fixture.Messages.Add(new Message
        {
            MessagesId = replyTargetId,
            ConversationsId = Guid.NewGuid(),
            SenderUserId = fixture.FreelancerUserId,
            MessageType = (int)MessageType.Text,
            Content = "different conversation",
            ClientMessageId = "other-1",
            SentAt = fixture.Now
        });

        var handler = new SendMessageCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SendMessageCommand(
                    fixture.ClientUserId,
                    new SendMessageRequest(
                        fixture.ConversationId,
                        "reply-1",
                        "reply",
                        replyTargetId,
                        [])),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetConversationMessages_ReturnsNewestPageChronological()
    {
        var fixture = new MessageFixture();
        var first = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.ClientUserId,
            MessageType = (int)MessageType.Text,
            Content = "first",
            ClientMessageId = "first",
            SentAt = fixture.Now.AddMinutes(1)
        };
        var second = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.FreelancerUserId,
            MessageType = (int)MessageType.Text,
            Content = "second",
            ClientMessageId = "second",
            SentAt = fixture.Now.AddMinutes(2)
        };
        var third = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.ClientUserId,
            MessageType = (int)MessageType.Text,
            Content = "third",
            ClientMessageId = "third",
            SentAt = fixture.Now.AddMinutes(3)
        };

        fixture.Messages.AddRange(first, second, third);

        var handler = new GetConversationMessagesQueryHandler(fixture.Context);

        var messages = await handler.Handle(
            new GetConversationMessagesQuery(fixture.ConversationId, fixture.ClientUserId, null, 2),
            CancellationToken.None);

        Assert.Collection(
            messages,
            message => Assert.Equal(second.MessagesId, message.MessageId),
            message => Assert.Equal(third.MessagesId, message.MessageId));
    }

    [Fact]
    public async Task GetMessagesAround_DeletedMessageSuppressesAttachmentsAndSchedulePayload()
    {
        var fixture = new MessageFixture();
        var deletedMessage = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.ClientUserId,
            MessageType = (int)MessageType.Schedule,
            Content = "Deleted schedule",
            Metadata = "{}",
            ClientMessageId = "deleted-schedule",
            SentAt = fixture.Now.AddMinutes(1),
            EditedAt = fixture.Now.AddMinutes(2),
            DeletedForEveryoneAt = fixture.Now.AddMinutes(3)
        };
        fixture.Messages.Add(deletedMessage);
        fixture.Attachments.Add(new MessageAttachment
        {
            MessageAttachmentsId = Guid.NewGuid(),
            MessagesId = deletedMessage.MessagesId,
            FileName = "private.pdf",
            FileUrl = "https://files.example/private.pdf",
            StorageProvider = "cloudinary",
            MimeType = "application/pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 256,
            CreatedAt = fixture.Now
        });

        var handler = new GetMessagesAroundQueryHandler(fixture.Context);

        var messages = await handler.Handle(
            new GetMessagesAroundQuery(
                fixture.ConversationId,
                deletedMessage.MessagesId,
                fixture.ClientUserId),
            CancellationToken.None);

        var response = Assert.Single(messages);
        Assert.True(response.IsDeleted);
        Assert.Null(response.Content);
        Assert.Null(response.Metadata);
        Assert.Null(response.EditedAt);
        Assert.Empty(response.Attachments);
        Assert.Null(response.Schedule);
    }

    [Fact]
    public async Task MarkConversationAsRead_ResetsUnreadAndEmitsReadAndConversationUpdateEvents()
    {
        var fixture = new MessageFixture();
        var messageId = Guid.NewGuid();
        fixture.Messages.Add(new Message
        {
            MessagesId = messageId,
            ConversationsId = fixture.ConversationId,
            SenderUserId = fixture.ClientUserId,
            MessageType = (int)MessageType.Text,
            Content = "hello",
            ClientMessageId = "read-1",
            SentAt = fixture.Now
        });
        fixture.FreelancerParticipant.UnreadCount = 3;

        var notifier = new CapturingChatRealtimeNotifier();
        var handler = new MarkConversationAsReadCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            notifier);

        var result = await handler.Handle(
            new MarkConversationAsReadCommand(
                fixture.ConversationId,
                messageId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, fixture.FreelancerParticipant.UnreadCount);
        Assert.Equal(messageId, fixture.FreelancerParticipant.LastReadMessageId);

        Assert.Contains(
            notifier.ConversationEvents,
            realtimeEvent => realtimeEvent.EventName == "ConversationRead" &&
                realtimeEvent.ConversationId == fixture.ConversationId);
        Assert.Contains(
            notifier.UserEvents,
            realtimeEvent => realtimeEvent.EventName == "ConversationUpdated" &&
                realtimeEvent.UserId == fixture.FreelancerUserId);
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
            Attachments = Context.AddSet<MessageAttachment>();
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
        public TestDbSet<MessageAttachment> Attachments { get; }
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
