namespace Domain.Enums.Delivery;

/// <summary>
/// Discriminates non-schedule DeliveryOutbox rows (ScheduleId is null). FinalContractEmail is
/// the default (0) so existing rows created before this enum existed keep routing through the
/// original e-sign final contract email path.
/// </summary>
public enum DeliveryOutboxType
{
    FinalContractEmail = 0,
    MilestoneSubmission = 1,
    ESignDocumentRevision = 2,
    NotificationStateRevision = 3,
    ConversationInboxRevision = 4,
    ProjectReceiptRevision = 5,
    GenericNotification = 6
}
