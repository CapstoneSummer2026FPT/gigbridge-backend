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
        return new JobPostSummaryDto(
            JobPostsId: jobPost.JobPostsId,
            Title: jobPost.Title,
            DescriptionPreview: CreatePreview(jobPost.Description),
            BudgetMin: jobPost.BudgetMin,
            BudgetMax: jobPost.BudgetMax,
            CreatedAt: jobPost.CreatedAt,
            EloPoints: jobPost.ClientProfiles?.User?.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
            SkillNames: jobPost.JobPostSkills
                .Where(jobPostSkill => jobPostSkill.Skills is not null)
                .Select(jobPostSkill => jobPostSkill.Skills.Name)
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
