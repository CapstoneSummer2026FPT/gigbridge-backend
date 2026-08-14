using Application.Features.JobPosts.Common.DTOs;

namespace Application.Features.Seo.PublicMarketplace.DTOs;

/// <summary>
/// Anonymous job-post contract. It intentionally excludes contact details,
/// attachments, and other data that is only available to authenticated flows.
/// </summary>
public sealed record PublicJobPostDetailDto(
    Guid JobPostsId,
    Guid ClientProfilesId,
    Guid UserId,
    string FullName,
    string? Avatar,
    string? ClientFullName,
    string Title,
    string Description,
    Guid? MajorCategoryId,
    Guid? MajorId,
    string? MajorName,
    Guid? CategoryId,
    string? CategoryName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    string? Location,
    int Status,
    int? Visibility,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int EloPoints,
    List<JobPostSkillDto> Skills,
    List<string> CustomSkillNames,
    List<JobPostMilestonePlanDto> MilestonePlans,
    bool HasAiInterview);
