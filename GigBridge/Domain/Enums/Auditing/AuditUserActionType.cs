namespace Domain.Enums.Auditing;

public enum AuditUserActionType
{
    ConfirmedParticipation = 0,
    SignedEsignContract = 1,
    RequestedEarlyStart = 2,
    MilestoneSubmitted = 3,
    EscrowFunded = 4,
    MilestoneApproved = 5,
    ReportCreated = 6,
    DisputeCreated = 7,
    DisputeEscalated = 8
}
