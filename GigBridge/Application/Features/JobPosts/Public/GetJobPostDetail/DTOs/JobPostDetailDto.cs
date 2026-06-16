using Application.Features.JobPosts.Common.DTOs;
using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;

public record JobPostDetailDto(
    Guid JobPostsId,
    Guid ClientProfilesId,
    string Title,
    string Description,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? EstimatedDuration,
    int? MaxHires,
    string? Location,
    DateTime? EndDate,
    DateTime CreatedAt,
    List<JobPostSkillDto> Skills,
    List<AttachmentDto> Attachments
);
