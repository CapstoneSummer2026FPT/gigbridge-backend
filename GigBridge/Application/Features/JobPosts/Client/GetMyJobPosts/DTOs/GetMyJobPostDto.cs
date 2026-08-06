using Application.Features.JobPosts.Client.Common;

namespace Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;

public sealed class GetMyJobPostDto
{
    public Guid JobPostsId { get; set; }

    public Guid ClientProfilesId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? MajorCategoryId { get; set; }

    public Guid? MajorId { get; set; }

    public string? MajorName { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public List<GetMyJobPostSkillDto> Skills { get; set; } = new();

    public List<string> CustomSkillNames { get; set; } = new();

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public string? Location { get; set; }

    public int Status { get; set; }

    public int? Visibility { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsAigenerated { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? FeaturedUntil { get; set; }

    public bool HasAiInterview { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ProposalCount { get; set; }

    public JobPostSetupProgressDto? SetupProgress { get; set; }
}
