namespace Application.Features.JobPosts.Freelancer.GetProfileMatchedJobPosts.DTOs;

public sealed record ProfileMatchedJobPostsRequest(
    int PageIndex = 1,
    int PageSize = 20,
    List<Guid>? MajorCategoryIds = null,
    string? Search = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? Skills = null,
    string? WorkType = null,
    int? PostedWithinDays = null,
    string? SortBy = null,
    bool SortDesc = true,
    Guid? SearchEventId = null);
