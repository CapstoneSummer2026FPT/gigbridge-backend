using Application.Features.JobPosts.Common.DTOs;

namespace Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;

public sealed record SaveDraftJobPostRequest(
    string? Title,
    string? Description,
    Guid? MajorCategoryId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    int? Visibility,
    DateTime? EndDate,
    bool? IsAigenerated,
    List<Guid>? SkillIds,
    List<string>? CustomSkillNames,
    List<SaveDraftJobPostQuestionRequest>? Questions,
    List<JobPostMilestonePlanDto>? MilestonePlans = null
);

public sealed record SaveDraftJobPostQuestionRequest(
    string? QuestionText,
    int OrderIndex,
    bool IsRequired
);
