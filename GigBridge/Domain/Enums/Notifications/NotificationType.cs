namespace Domain.Enums.Notifications;

public enum NotificationType
{
    NewJob = 0,
    ProposalReceived = 1,
    ProposalStatusChanged = 2,
    ContractStarted = 3,
    MilestoneUpdated = 4,
    PaymentProofUploaded = 5,
    PaymentConfirmed = 6,
    ChatMessage = 7,
    DisputeUpdate = 8,
    ReviewReceived = 9,
    SystemAlert = 10,
    AIInterviewInvite = 11,
    SubscriptionExpiring = 12,
    Schedule = 13,
    SubscriptionActivated = 14,
    SubscriptionCancelled = 15,
    PromotionActivated = 16,
    PromotionExpired = 17,
    RankProtectionActivated = 18,
    RankProtectionExpired = 19,
    ReportUpdate = 20,
    ReviewRequested = 21,

    EloPointsUpdated = 22,
    EloAppealStatusChanged = 23,
    ReceiptReady = 24,
    ReceiptFailed = 25
}
