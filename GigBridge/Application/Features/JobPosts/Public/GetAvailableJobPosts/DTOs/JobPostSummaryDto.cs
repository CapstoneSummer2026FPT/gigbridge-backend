using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;

public record JobPostSummaryDto(
    Guid JobPostsId,
    string Title,
    string DescriptionPreview,
    int BudgetType,
    decimal? BudgetMin,
    decimal? BudgetMax,
    int? ExperienceLevelRequired,
    DateTime CreatedAt,
    int EloPoints,
    List<string> SkillNames
);
