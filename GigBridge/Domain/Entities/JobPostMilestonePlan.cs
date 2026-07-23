namespace Domain.Entities;

public sealed class JobPostMilestonePlan
{
    public Guid JobPostMilestonePlanId { get; set; }
    public Guid JobPostsId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Deliverables { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public JobPost JobPost { get; set; } = null!;
    public ICollection<JobPostWorkItem> WorkItems { get; set; } = new List<JobPostWorkItem>();
}
