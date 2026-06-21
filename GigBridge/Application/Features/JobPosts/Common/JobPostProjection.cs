using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using Domain.Services;

namespace Application.Features.JobPosts.Common;

internal static class JobPostProjection
{
    public static List<JobPostSummaryDto> ToSummaryDtos(IEnumerable<JobPost> jobPosts)
    {
        return jobPosts.Select(ToSummaryDto).ToList();
    }

    private static JobPostSummaryDto ToSummaryDto(JobPost jobPost)
    {
        var officialSkills = jobPost.JobPostSkills
            .Where(jobPostSkill => jobPostSkill.Skills is not null)
            .Select(jobPostSkill => new JobPostSkillDto(jobPostSkill.SkillsId, jobPostSkill.Skills.Name))
            .ToList();

        return new JobPostSummaryDto(
            JobPostsId: jobPost.JobPostsId,
            Title: jobPost.Title,
            DescriptionPreview: CreatePreview(jobPost.Description),
            MajorCategoryId: jobPost.MajorCategoryId,
            MajorId: jobPost.MajorCategory?.MajorId,
            MajorName: jobPost.MajorCategory?.Major?.Name,
            CategoryId: jobPost.MajorCategory?.CategoryId,
            CategoryName: jobPost.MajorCategory?.Category?.Name,
            BudgetMin: jobPost.BudgetMin,
            BudgetMax: jobPost.BudgetMax,
            CreatedAt: jobPost.CreatedAt,
            Status: jobPost.Status,
            Visibility: jobPost.Visibility,
            EloPoints: jobPost.ClientProfiles?.User?.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
            Skills: officialSkills,
            CustomSkillNames: jobPost.CustomSkillNames.ToList(),
            SkillNames: officialSkills
                .Select(skill => skill.SkillName)
                .ToList());
    }

    private static string CreatePreview(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return string.Empty;
        }

        return description.Length > 200 ? $"{description[..200]}..." : description;
    }
}
