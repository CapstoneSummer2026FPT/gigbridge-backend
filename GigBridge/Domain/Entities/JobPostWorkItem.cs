namespace Domain.Entities;

public sealed class JobPostWorkItem
{
    public Guid JobPostWorkItemId { get; set; }
    public Guid JobPostMilestonePlanId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }

    public JobPostMilestonePlan MilestonePlan { get; set; } = null!;
}
