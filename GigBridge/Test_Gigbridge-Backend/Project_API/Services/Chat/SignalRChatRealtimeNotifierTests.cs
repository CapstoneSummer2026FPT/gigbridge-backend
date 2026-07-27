using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Project_API.Hubs;
using Project_API.Services.Chat;

namespace Test_Gigbridge_Backend.Project_API.Services.Chat;

public sealed class SignalRChatRealtimeNotifierTests
{
    [Fact]
    public async Task SendUsersEventAsync_TargetsAuthenticatedSignalRUsers()
    {
        var hubContext = Substitute.For<IHubContext<ChatHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(clientProxy);

        var notifier = new SignalRChatRealtimeNotifier(
            hubContext,
            Substitute.For<ILogger<SignalRChatRealtimeNotifier>>());
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var payload = new { conversationId = Guid.NewGuid(), content = "hello" };

        await notifier.SendUsersEventAsync(
            [clientUserId, freelancerUserId, clientUserId],
            "ReceiveMessage",
            payload,
            CancellationToken.None);

        clients.Received(1).Users(Arg.Is<IReadOnlyList<string>>(userIds =>
            userIds.Count == 2 &&
            userIds.Contains(clientUserId.ToString()) &&
            userIds.Contains(freelancerUserId.ToString())));
        await clientProxy.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Is<object?[]>(arguments => arguments.Length == 1 && ReferenceEquals(arguments[0], payload)),
            CancellationToken.None);
    }

    [Fact]
    public async Task SendUserEventAsync_TargetsEveryConnectionForAuthenticatedUser()
    {
        var hubContext = Substitute.For<IHubContext<ChatHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        var userId = Guid.NewGuid();
        clients.User(userId.ToString()).Returns(clientProxy);

        var notifier = new SignalRChatRealtimeNotifier(
            hubContext,
            Substitute.For<ILogger<SignalRChatRealtimeNotifier>>());

        await notifier.SendUserEventAsync(
            userId,
            "ConversationUpdated",
            new { conversationId = Guid.NewGuid() },
            CancellationToken.None);

        clients.Received(1).User(userId.ToString());
        await clientProxy.Received(1).SendCoreAsync(
            "ConversationUpdated",
            Arg.Any<object?[]>(),
            CancellationToken.None);
    }
}
