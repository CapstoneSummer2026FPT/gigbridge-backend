using System;

namespace Domain.Entities;

/// <summary>
/// Append-only audit trail of notable Client/Freelancer actions during a contract's
/// lifecycle (participation, signing, milestone work, escrow funding, reports, disputes).
/// Written only after the corresponding business operation succeeds. Used by Admins to
/// reconstruct the chronological sequence of events before/after a dispute.
/// </summary>
public partial class AuditLogWorkSpace
{
    public Guid AuditLogWorkSpaceId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Enum UserRole: 0=Client, 1=Freelancer, 2=Admin (only Client/Freelancer occur here)
    /// </summary>
    public int UserRole { get; set; }

    /// <summary>
    /// Enum AuditUserActionType: 0=ConfirmedParticipation, 1=SignedEsignContract,
    /// 2=RequestedEarlyStart, 3=MilestoneSubmitted, 4=EscrowFunded, 5=MilestoneApproved,
    /// 6=ReportCreated, 7=DisputeCreated, 8=DisputeEscalated
    /// </summary>
    public int ActionType { get; set; }

    public Guid ContractId { get; set; }

    public Guid? JobPostId { get; set; }

    public Guid? MilestoneId { get; set; }

    public Guid? ReportId { get; set; }

    public Guid? DisputeId { get; set; }

    /// <summary>
    /// Optional pointer to a related resource not covered by the fields above
    /// (e.g. an EsignDocument or EscrowTransaction id), paired with RelatedEntityType.
    /// </summary>
    public Guid? RelatedEntityId { get; set; }

    public string? RelatedEntityType { get; set; }

    public string Description { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Contract Contract { get; set; } = null!;

    public virtual Milestone? Milestone { get; set; }

    public virtual ReportContract? Report { get; set; }

    public virtual Dispute? Dispute { get; set; }
}
