using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Questions;

internal static class JobPostQuestionCommandHelper
{
    public static async Task<ClientProfile> GetClientProfileAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        return clientProfile
            ?? throw new NotFoundException("Client profile does not exist.");
    }

    public static async Task<JobPost> GetClientOwnedJobPostAsync(
        IApplicationDbContext context,
        Guid jobPostId,
        Guid clientProfileId,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == jobPostId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfileId)
        {
            throw new ForbiddenAccessException("You do not have permission to modify this job post.");
        }

        return jobPost;
    }

    public static void EnsureDraft(JobPost jobPost)
    {
        if (jobPost.Status != 0)
        {
            throw new BadRequestException("Questions can only be modified while the job post is draft.");
        }
    }

    public static JobPostQuestionDto ToDto(JobPostQuestion question)
    {
        return new JobPostQuestionDto(
            question.JobPostQuestionsId,
            question.JobPostsId,
            question.QuestionText,
            question.OrderIndex,
            question.IsRequired,
            question.CreatedAt,
            question.UpdatedAt);
    }
}
