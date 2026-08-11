using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Messages.CreateGoogleMeet;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Chat;

public class CreateGoogleMeetMessageCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesRoomAndSendsItsLinkAsAuthenticatedUser()
    {
        await using var context = CreateContext();
        var (userId, conversationId) = SeedConversation(context);
        var oauth = Substitute.For<IGoogleMeetOAuthService>();
        var meetApi = Substitute.For<IGoogleMeetApiClient>();
        var sender = Substitute.For<ISender>();
        var response = new MessageResponse(
            Guid.NewGuid(), conversationId, userId, (int)MessageType.Text,
            "https://meet.google.com/abc-defg-hij", null, null, "client-message-1",
            DateTime.UtcNow, null, false, []);

        oauth.GetAccessTokenAsync(userId, Arg.Any<CancellationToken>())
            .Returns("access-token");
        meetApi.CreateSpaceAsync("access-token", Arg.Any<CancellationToken>())
            .Returns(new CreateMeetSpaceResult(
                true, false, "spaces/test", "https://meet.google.com/abc-defg-hij", null));
        sender.Send(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var handler = new CreateGoogleMeetMessageCommandHandler(context, oauth, meetApi, sender);
        var result = await handler.Handle(
            new CreateGoogleMeetMessageCommand(
                userId,
                new CreateGoogleMeetMessageRequest(conversationId, "client-message-1")),
            default);

        Assert.Same(response, result);
        await sender.Received(1).Send(
            Arg.Is<SendMessageCommand>(command =>
                command.UserId == userId &&
                command.Request.ConversationId == conversationId &&
                command.Request.ClientMessageId == "client-message-1" &&
                command.Request.Content == "https://meet.google.com/abc-defg-hij"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RejectsNonParticipantBeforeCreatingRoom()
    {
        await using var context = CreateContext();
        var (_, conversationId) = SeedConversation(context);
        var oauth = Substitute.For<IGoogleMeetOAuthService>();
        var meetApi = Substitute.For<IGoogleMeetApiClient>();
        var sender = Substitute.For<ISender>();
        var handler = new CreateGoogleMeetMessageCommandHandler(context, oauth, meetApi, sender);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new CreateGoogleMeetMessageCommand(
                Guid.NewGuid(),
                new CreateGoogleMeetMessageRequest(conversationId, "client-message-1")),
            default));

        await meetApi.DidNotReceive().CreateSpaceAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().Send(
            Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>());
    }

    private static GigbridgeDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (Guid UserId, Guid ConversationId) SeedConversation(GigbridgeDbContext context)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Client",
            Email = "client@test.local",
            Role = (int)UserRole.Client,
            IsActive = true,
            CreatedAt = now
        };
        var conversation = new Conversation
        {
            ConversationsId = Guid.NewGuid(),
            CreatedByUserId = user.UserId,
            ConversationType = (int)ConversationType.JobNegotiation,
            Status = (int)ConversationStatus.Active,
            CreatedAt = now
        };
        var participant = new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            UserId = user.UserId,
            ParticipantRole = (int)ParticipantRole.Client,
            JoinedAt = now,
            User = user,
            Conversations = conversation
        };
        context.AddRange(user, conversation, participant);
        context.SaveChanges();
        return (user.UserId, conversation.ConversationsId);
    }
}
