namespace Application.Features.SavedJobs.Freelancer.GetMySavedJobs.DTOs;

public class SavedJobDto
{
    public Guid SavedJobId { get; set; }

    public Guid JobPostId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CategoryName { get; set; }

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public int Status { get; set; }

    public int? Visibility { get; set; }

    public DateTime JobCreatedAt { get; set; }

    public DateTime SavedAt { get; set; }
}