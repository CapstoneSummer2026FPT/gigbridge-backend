namespace Domain.Entities;

public sealed class UserViolation
{
    public Guid UserViolationId { get; set; }
    public Guid UserId { get; set; }
    public int SourceType { get; set; }
    public Guid? DisputeId { get; set; }
    public Guid? ReportId { get; set; }
    public Guid? ManualActionId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? MilestoneId { get; set; }
    public int ViolationNumber { get; set; }
    public int ViolationType { get; set; }
    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public int ActionTaken { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public Guid CreatedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public Dispute? Dispute { get; set; }
    public Report? Report { get; set; }
    public Contract? Contract { get; set; }
    public Milestone? Milestone { get; set; }
    public User CreatedByAdmin { get; set; } = null!;
}
