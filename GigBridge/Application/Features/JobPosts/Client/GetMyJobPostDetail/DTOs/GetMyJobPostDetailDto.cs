using Application.Features.JobPosts.Client.Common;
using Application.Features.JobPosts.Common.DTOs;

namespace Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;

public sealed class GetMyJobPostDetailDto
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

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public string? Location { get; set; }

    public int? Visibility { get; set; }

    public int Status { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<JobPostSkillDto> Skills { get; set; } = new();

    public List<string> CustomSkillNames { get; set; } = new();

    public List<AttachmentDto> Attachments { get; set; } = new();

    public int ProposalCount { get; set; }

    public JobPostSetupProgressDto? SetupProgress { get; set; }
}
