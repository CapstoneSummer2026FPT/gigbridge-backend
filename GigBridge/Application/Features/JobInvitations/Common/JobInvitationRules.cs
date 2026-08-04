using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobInvitations.Common;

public static class JobInvitationRules
{
    public const int OpenJobPostStatus = 1;

    public static async Task<ClientProfile> GetClientProfileAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        return profile ?? throw new ForbiddenAccessException("Only clients can manage sent job invitations.");
    }

    public static async Task<FreelancerProfile> GetFreelancerProfileAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        return profile ?? throw new ForbiddenAccessException("Only freelancers can manage received job invitations.");
    }

    public static async Task<JobPost> GetOwnedOpenJobPostAsync(
        IApplicationDbContext context,
        Guid jobPostId,
        Guid clientProfileId,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .FirstOrDefaultAsync(item => item.JobPostsId == jobPostId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfileId)
        {
            throw new ForbiddenAccessException("You do not own this job post.");
        }

        EnsureJobPostIsOpen(jobPost);

        return jobPost;
    }

    public static void EnsureJobPostIsOpen(JobPost jobPost)
    {
        if (jobPost.Status != OpenJobPostStatus)
        {
            throw new BadRequestException("Only open job posts can be used for invitations.");
        }
    }

    public static void EnsureCanRespond(JobInvitation invitation)
    {
        if (invitation.Status != (int)JobInvitationStatus.Pending &&
            invitation.Status != (int)JobInvitationStatus.Viewed)
        {
            throw new BadRequestException("This invitation can no longer be updated.");
        }
    }

    public static async Task<JobInvitation> GetOwnedSentInvitationAsync(
        IApplicationDbContext context,
        Guid invitationId,
        Guid clientProfileId,
        CancellationToken cancellationToken)
    {
        var invitation = await context.Set<JobInvitation>()
            .FirstOrDefaultAsync(item => item.JobInvitationsId == invitationId, cancellationToken);

        if (invitation is null)
        {
            throw new NotFoundException("Job invitation does not exist.");
        }

        if (invitation.ClientProfilesId != clientProfileId)
        {
            throw new ForbiddenAccessException("You do not own this job invitation.");
        }

        return invitation;
    }

    public static async Task<JobInvitation> GetOwnedReceivedInvitationAsync(
        IApplicationDbContext context,
        Guid invitationId,
        Guid freelancerProfileId,
        CancellationToken cancellationToken)
    {
        var invitation = await context.Set<JobInvitation>()
            .FirstOrDefaultAsync(item => item.JobInvitationsId == invitationId, cancellationToken);

        if (invitation is null)
        {
            throw new NotFoundException("Job invitation does not exist.");
        }

        if (invitation.FreelancerProfilesId != freelancerProfileId)
        {
            throw new ForbiddenAccessException("You do not own this job invitation.");
        }

        return invitation;
    }

    public static string? CleanMessage(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
