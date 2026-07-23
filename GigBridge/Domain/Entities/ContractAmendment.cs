namespace Domain.Entities;

public sealed class ContractAmendment
{
    public Guid ContractAmendmentId { get; set; }
    public Guid ContractsId { get; set; }
    public Guid ContractChangeRequestId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public int RevisionNumber { get; set; }
    public string Reason { get; set; } = null!;
    public decimal OriginalTotalBudget { get; set; }
    public decimal ProposedTotalBudget { get; set; }
    public decimal BudgetDelta { get; set; }
    public string? ReviewNote { get; set; }
    public string? DocumentSnapshotJson { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public ContractChangeRequest ChangeRequest { get; set; } = null!;
    public ICollection<ContractAmendmentMilestone> Milestones { get; set; } = new List<ContractAmendmentMilestone>();
    public ICollection<ContractAmendmentSignature> Signatures { get; set; } = new List<ContractAmendmentSignature>();
}
