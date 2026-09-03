namespace Application.Common.InternalServices.Delivery.Models;

/// <summary>
/// Payload for a <c>DeliveryOutbox</c> row of type <see cref="Domain.Enums.Delivery.DeliveryOutboxType.GenericNotification"/>.
/// Carries just enough to look the already-persisted row back up at dispatch time — the outbox
/// only durably retries the real-time push, the <c>Notification</c>/<c>BroadcastNotificationRecipient</c>
/// row itself is created up front by <c>NotificationService</c> before this delivery is enqueued.
/// </summary>
public sealed record GenericNotificationDeliveryPayload(
    Guid UserId,
    Guid? NotificationId,
    Guid? BroadcastNotificationId,
    Guid? BroadcastNotificationRecipientId);
