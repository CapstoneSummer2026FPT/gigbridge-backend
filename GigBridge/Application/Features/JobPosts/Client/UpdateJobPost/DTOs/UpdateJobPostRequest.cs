namespace Application.Features.JobPosts.Client.UpdateJobPost.DTOs;

public class UpdateJobPostRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Enum BudgetType: 0=Fixed, 1=Hourly
    /// </summary>
    public int BudgetType { get; set; }

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public int? MaxHires { get; set; }

    /// <summary>
    /// Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert
    /// </summary>
    public int? ExperienceLevelRequired { get; set; }

    /// <summary>
    /// Enum LocationType: 0=Remote, 1=OnSite, 2=Hybrid
    /// </summary>
    public int? LocationType { get; set; }

    public string? Location { get; set; }

    public DateTime? ApplicationDeadline { get; set; }

    public List<Guid> SkillIds { get; set; } = new();
}