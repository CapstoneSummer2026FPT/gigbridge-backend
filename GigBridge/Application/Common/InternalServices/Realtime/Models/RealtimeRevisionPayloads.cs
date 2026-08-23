using Application.Common.InternalServices.Notifications.Models;

namespace Application.Common.InternalServices.Realtime.Models;

public sealed record NotificationStateChangedPayload(
    int Revision,
    int UnreadCount,
    string ChangeKind,
    NotificationDto? Item = null,
    Guid? NotificationId = null);

public sealed record ConversationInboxRevisionChangedPayload(
    int Revision,
    int UnreadCount,
    Guid? ConversationId,
    string ChangeKind);

public sealed record ProjectReceiptRevisionChangedPayload(
    Guid ReceiptId,
    Guid ContractId,
    int Revision,
    string ChangeKind);

public static class RealtimeRevisionEvents
{
    public const string NotificationStateChanged = "NotificationStateChanged";
    public const string ConversationInboxRevisionChanged = "ConversationInboxRevisionChanged";
    public const string ProjectReceiptRevisionChanged = "ProjectReceiptRevisionChanged";
}
