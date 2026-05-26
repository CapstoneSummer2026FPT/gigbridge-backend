namespace Application.Features.Admin.JobPosts.Dto;

public class JobPostDto
{
    public Guid JobPostId { get; init; }
    public Guid ClientProfileId { get; init; }
    public string? ClientCompanyName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public int Status { get; init; }
    public decimal? BudgetMin { get; init; }
    public decimal? BudgetMax { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class JobPostDetailDto : JobPostDto
{
    public string Description { get; init; } = string.Empty;
    public int ProposalCount { get; init; }
}

public sealed class JobStatusRequestDto
{
    public int Status { get; set; }
    public string? Reason { get; set; }
}
