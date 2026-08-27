namespace Application.Common.InternalServices.Chat.Interfaces;

public interface IUserRealtimeEventSender
{
    Task SendAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
