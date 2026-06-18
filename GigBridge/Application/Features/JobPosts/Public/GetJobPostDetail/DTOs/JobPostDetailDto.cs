using Application.Features.JobPosts.Common.DTOs;
using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;

public record JobPostDetailDto(
    Guid JobPostsId,
    Guid ClientProfilesId,
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
    int? MaxHires,
    string? Location,
    DateTime? EndDate,
    DateTime CreatedAt,
    int EloPoints,
    List<JobPostSkillDto> Skills,
    List<string> CustomSkillNames,
    List<AttachmentDto> Attachments
);
