using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Client.CreateJobPost.DTOs;

public record CreateJobPostRequest(
    string Title,
    string Description,
    Guid? MajorCategoryId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    int? MaxHires,
    string? Location,
    int? Visibility,
    DateTime? EndDate,
    List<Guid>? SkillIds,
    List<string>? CustomSkillNames
);