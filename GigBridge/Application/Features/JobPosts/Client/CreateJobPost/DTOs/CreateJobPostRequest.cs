using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Client.CreateJobPost.DTOs;

public record CreateJobPostRequest(
    string Title,
    string Description,
    Guid? CategoryId,
    int BudgetType,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    int? MaxHires,
    int? ExperienceLevelRequired,
    int? LocationType,
    string? Location,
    int? Visibility,
    DateTime? ApplicationDeadline,
    List<Guid> SkillIds
);