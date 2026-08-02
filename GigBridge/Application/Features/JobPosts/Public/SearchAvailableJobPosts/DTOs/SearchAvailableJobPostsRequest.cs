namespace Application.Features.JobPosts.Public.SearchAvailableJobPosts.DTOs;

public sealed record SearchAvailableJobPostsRequest(
    int PageIndex = 1,
    int PageSize = 12,
    string? Search = null,
    List<Guid>? SkillIds = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? SortBy = null,
    bool SortDesc = true,
    string? Category = null,
    string? Skills = null,
    string? WorkType = null,
    int? PostedWithinDays = null,
    bool AiOnly = false,
    Guid? SearchEventId = null);
