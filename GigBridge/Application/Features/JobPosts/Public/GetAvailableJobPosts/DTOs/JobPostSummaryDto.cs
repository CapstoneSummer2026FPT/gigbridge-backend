using Application.Features.JobPosts.Common.DTOs;
using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;

public record JobPostSummaryDto(
    Guid JobPostsId,
    string Title,
    string DescriptionPreview,
    Guid? MajorCategoryId,
    Guid? MajorId,
    string? MajorName,
    Guid? CategoryId,
    string? CategoryName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateTime CreatedAt,
    int Status,
    int? Visibility,
    int EloPoints,
    List<JobPostSkillDto> Skills,
    List<string> CustomSkillNames,
    List<string> SkillNames
);
