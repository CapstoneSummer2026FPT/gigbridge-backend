using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobInvitations.Client.BulkCreateInvitations.Commands;

public sealed class BulkCreateJobInvitationsCommandHandler
    : IRequestHandler<BulkCreateJobInvitationsCommand, BulkJobInvitationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<BulkCreateJobInvitationsCommandHandler> _logger;

    public BulkCreateJobInvitationsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        ILogger<BulkCreateJobInvitationsCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<BulkJobInvitationResultDto> Handle(
        BulkCreateJobInvitationsCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var jobPostIds = command.Request.JobPostIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var freelancerProfileIds = command.Request.FreelancerProfileIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var jobsById = await _context.Set<JobPost>()
            .Where(jobPost => jobPostIds.Contains(jobPost.JobPostsId))
            .ToDictionaryAsync(jobPost => jobPost.JobPostsId, cancellationToken);

        var freelancersById = await _context.Set<FreelancerProfile>()
            .Where(profile => freelancerProfileIds.Contains(profile.FreelancerProfilesId))
            .ToDictionaryAsync(profile => profile.FreelancerProfilesId, cancellationToken);

        var existingPairs = (await _context.Set<JobInvitation>()
                .AsNoTracking()
                .Where(invitation =>
                    jobPostIds.Contains(invitation.JobPostsId) &&
                    freelancerProfileIds.Contains(invitation.FreelancerProfilesId))
                .Select(invitation => new
                {
                    invitation.JobPostsId,
                    invitation.FreelancerProfilesId
                })
                .ToListAsync(cancellationToken))
            .Select(pair => (pair.JobPostsId, pair.FreelancerProfilesId))
            .ToHashSet();

        var result = new BulkJobInvitationResultDto();
        var createdIds = new List<Guid>();
        var notificationPayloads = new List<(Guid FreelancerUserId, Guid InvitationId, string JobTitle)>();
        var now = _dateTimeService.UtcNow;
        var message = JobInvitationRules.CleanMessage(command.Request.Message);

        foreach (var jobPostId in jobPostIds)
        {
            jobsById.TryGetValue(jobPostId, out var jobPost);

            foreach (var freelancerProfileId in freelancerProfileIds)
            {
                if (jobPost is null)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Job post does not exist.");
                    continue;
                }

                if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "You do not own this job post.");
                    continue;
                }

                if (jobPost.Status != JobInvitationRules.OpenJobPostStatus)
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Job post is not open.");
                    continue;
                }

                if (!freelancersById.TryGetValue(freelancerProfileId, out var freelancerProfile))
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "Freelancer profile does not exist.");
                    continue;
                }

                if (existingPairs.Contains((jobPostId, freelancerProfileId)))
                {
                    AddSkipped(result, jobPostId, freelancerProfileId, "This freelancer was already invited to this job post.");
                    continue;
                }

                var invitation = new JobInvitation
                {
                    JobInvitationsId = Guid.NewGuid(),
                    JobPostsId = jobPostId,
                    ClientProfilesId = clientProfile.ClientProfilesId,
                    FreelancerProfilesId = freelancerProfileId,
                    Status = (int)JobInvitationStatus.Pending,
                    Message = message,
                    ExpiresAt = command.Request.ExpiresAt,
                    CreatedAt = now
                };

                _context.Set<JobInvitation>().Add(invitation);
                createdIds.Add(invitation.JobInvitationsId);
                existingPairs.Add((jobPostId, freelancerProfileId));
                notificationPayloads.Add((freelancerProfile.UserId, invitation.JobInvitationsId, jobPost.Title));
            }
        }

        if (createdIds.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            result.Created = await _context.Set<JobInvitation>()
                .AsNoTracking()
                .Where(invitation => createdIds.Contains(invitation.JobInvitationsId))
                .OrderByDescending(invitation => invitation.CreatedAt)
                .ProjectToJobInvitationDto()
                .ToListAsync(cancellationToken);

            foreach (var payload in notificationPayloads)
            {
                await NotifyFreelancerAsync(payload.FreelancerUserId, payload.InvitationId, payload.JobTitle, cancellationToken);
            }
        }

        return result;
    }

    private static void AddSkipped(
        BulkJobInvitationResultDto result,
        Guid jobPostId,
        Guid freelancerProfileId,
        string reason)
    {
        result.Skipped.Add(new BulkJobInvitationSkipDto
        {
            JobPostId = jobPostId,
            FreelancerProfileId = freelancerProfileId,
            Reason = reason
        });
    }

    private async Task NotifyFreelancerAsync(
        Guid freelancerUserId,
        Guid invitationId,
        string jobTitle,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.CreateNotificationAsync(
                freelancerUserId,
                NotificationType.SystemAlert,
                "New job invitation",
                $"You were invited to apply for \"{jobTitle}\".",
                invitationId,
                "JobInvitation",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send job invitation notification {InvitationId}.", invitationId);
        }
    }
}
