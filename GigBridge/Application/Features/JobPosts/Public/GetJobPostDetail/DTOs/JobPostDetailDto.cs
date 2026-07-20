using Application.Features.JobPosts.Common.DTOs;
using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;

public record JobPostDetailDto(
    Guid JobPostsId,
    Guid ClientProfilesId,
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
    int EloPoints,
    List<JobPostSkillDto> Skills,
    List<string> CustomSkillNames,
    List<AttachmentDto> Attachments,
    List<JobPostMilestonePlanDto> MilestonePlans
);
