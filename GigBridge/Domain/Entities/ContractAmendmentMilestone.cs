namespace Domain.Entities;

public sealed class ContractAmendmentMilestone
{
    public Guid ContractAmendmentMilestoneId { get; set; }
    public Guid ContractAmendmentId { get; set; }
    public Guid? SourceMilestoneId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Deliverables { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }

    public ContractAmendment Amendment { get; set; } = null!;
    public ICollection<ContractAmendmentWorkItem> WorkItems { get; set; } = new List<ContractAmendmentWorkItem>();
}
