namespace Application.Features.JobPosts.Client.UpdateJobPost.DTOs;

public record UpdateJobPostRequest(
    string Title,
    string Description,
    Guid? MajorCategoryId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    int? Visibility,
    DateTime? EndDate,
    List<Guid>? SkillIds,
    List<string>? CustomSkillNames
);
