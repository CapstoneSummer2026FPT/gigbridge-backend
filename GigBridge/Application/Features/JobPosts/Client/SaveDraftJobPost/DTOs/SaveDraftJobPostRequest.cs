using System.Text.Json.Serialization;
using Application.Features.JobPosts.Common.DTOs;

namespace Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;

public sealed record SaveDraftJobPostRequest(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("majorCategoryId")] Guid? MajorCategoryId,
    [property: JsonPropertyName("budgetMin")] decimal? BudgetMin,
    [property: JsonPropertyName("budgetMax")] decimal? BudgetMax,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("estimatedDuration")] string? EstimatedDuration,
    [property: JsonPropertyName("visibility")] int? Visibility,
    [property: JsonPropertyName("endDate")] DateTime? EndDate,
    [property: JsonPropertyName("isAigenerated")] bool? IsAigenerated,
    [property: JsonPropertyName("skillIds")] List<Guid>? SkillIds,
    [property: JsonPropertyName("customSkillNames")] List<string>? CustomSkillNames,
    [property: JsonPropertyName("questions")] List<SaveDraftJobPostQuestionRequest>? Questions,
    [property: JsonPropertyName("milestonePlans")] List<JobPostMilestonePlanDto>? MilestonePlans = null
);

public sealed record SaveDraftJobPostQuestionRequest(
    [property: JsonPropertyName("questionText")] string? QuestionText,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("isRequired")] bool IsRequired
);
