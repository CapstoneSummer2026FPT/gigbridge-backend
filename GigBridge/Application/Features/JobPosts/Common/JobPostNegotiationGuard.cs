using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Common;

public static class JobPostNegotiationGuard
{
    public const int OpenStatus = 1;
    public const int AdminLockedVisibility = 3;

    public static bool IsEligibleForNegotiation(int status, int? visibility)
    {
        return status == OpenStatus && visibility != AdminLockedVisibility;
    }

    public static void EnsureEligibleForNegotiation(JobPost jobPost)
    {
        if (jobPost.Visibility == AdminLockedVisibility)
        {
            throw new BadRequestException("Job post is locked by an admin and cannot be negotiated.");
        }

        if (jobPost.Status != OpenStatus)
        {
            throw new BadRequestException("Job post is no longer open for negotiations.");
        }
    }

    public static async Task EnsureEligibleForNegotiationAsync(
        IApplicationDbContext context,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.JobPostsId == jobPostId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        EnsureEligibleForNegotiation(jobPost);
    }
}
