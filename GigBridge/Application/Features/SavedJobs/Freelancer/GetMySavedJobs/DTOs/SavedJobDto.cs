namespace Application.Features.SavedJobs.Freelancer.GetMySavedJobs.DTOs;

public class SavedJobSkillDto
{
    public Guid SkillId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class SavedJobDto
{
    public Guid SavedJobId { get; set; }

    public Guid JobPostId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? MajorCategoryId { get; set; }

    public Guid? MajorId { get; set; }

    public string? MajorName { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public List<SavedJobSkillDto> Skills { get; set; } = new();

    public List<string> CustomSkillNames { get; set; } = new();

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public int Status { get; set; }

    public int? Visibility { get; set; }

    public DateTime JobCreatedAt { get; set; }

    public DateTime SavedAt { get; set; }
}