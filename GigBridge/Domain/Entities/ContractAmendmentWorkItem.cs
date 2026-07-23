namespace Domain.Entities;

public sealed class ContractAmendmentWorkItem
{
    public Guid ContractAmendmentWorkItemId { get; set; }
    public Guid ContractAmendmentMilestoneId { get; set; }
    public Guid? SourceContractWorkItemId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }

    public ContractAmendmentMilestone Milestone { get; set; } = null!;
}
