using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Domain.Entities;
using Domain.Services;

namespace Application.Features.JobPosts.Common;

internal static class JobPostDetailProjection
{
    public static JobPostDetailDto ToDto(JobPost jobPost, bool hasAiInterview) =>
        new(
            JobPostsId: jobPost.JobPostsId,
            ClientProfilesId: jobPost.ClientProfilesId,
            ClientFullName: jobPost.ClientProfiles?.User?.FullName
                            ?? jobPost.ClientProfiles?.CompanyName,
            Title: jobPost.Title,
            Description: jobPost.Description,
            MajorCategoryId: jobPost.MajorCategoryId,
            MajorId: jobPost.MajorCategory?.MajorId,
            MajorName: jobPost.MajorCategory?.Major?.Name,
            CategoryId: jobPost.MajorCategory?.CategoryId,
            CategoryName: jobPost.MajorCategory?.Category?.Name,
            BudgetMin: jobPost.BudgetMin,
            BudgetMax: jobPost.BudgetMax,
            Currency: jobPost.Currency,
            EstimatedDuration: jobPost.EstimatedDuration,
            Location: jobPost.Location,
            Status: jobPost.Status,
            Visibility: jobPost.Visibility,
            EndDate: jobPost.EndDate,
            CreatedAt: jobPost.CreatedAt,
            EloPoints: jobPost.ClientProfiles?.User?.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
            Skills: jobPost.JobPostSkills
                .Where(jobPostSkill => jobPostSkill.Skills is not null)
                .Select(jobPostSkill => new JobPostSkillDto(jobPostSkill.SkillsId, jobPostSkill.Skills.Name))
                .ToList(),
            CustomSkillNames: jobPost.CustomSkillNames.ToList(),
            Attachments: jobPost.JobPostAttachments
                .Select(attachment => new AttachmentDto(attachment.JobPostAttachmentsId, attachment.FileUrl, attachment.FileName))
                .ToList(),
            HasAiInterview: hasAiInterview,
            MilestonePlans: JobPostPlanProjection.ToDtos(jobPost.JobPostMilestonePlans));
}
