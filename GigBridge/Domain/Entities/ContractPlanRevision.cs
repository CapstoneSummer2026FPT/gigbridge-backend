namespace Domain.Entities;

public sealed class ContractPlanRevision
{
    public Guid ContractPlanRevisionId { get; set; }
    public Guid ContractsId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Contract Contract { get; set; } = null!;
}
