namespace Domain.Entities;

public sealed class ContractWorkItem
{
    public Guid ContractWorkItemId { get; set; }
    public Guid MilestonesId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }
    public int Status { get; set; }
    public string? ProgressNote { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Milestone Milestone { get; set; } = null!;
}
