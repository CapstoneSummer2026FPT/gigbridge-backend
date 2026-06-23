using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobInvitations.Client.CreateInvitation.Commands;

public sealed class CreateJobInvitationCommandHandler
    : IRequestHandler<CreateJobInvitationCommand, JobInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreateJobInvitationCommandHandler> _logger;

    public CreateJobInvitationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        ILogger<CreateJobInvitationCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<JobInvitationDto> Handle(
        CreateJobInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var jobPost = await JobInvitationRules.GetOwnedOpenJobPostAsync(
            _context,
            command.Request.JobPostId,
            clientProfile.ClientProfilesId,
            cancellationToken);

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.FreelancerProfilesId == command.Request.FreelancerProfileId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var alreadyInvited = await _context.Set<JobInvitation>()
            .AnyAsync(invitation =>
                invitation.JobPostsId == jobPost.JobPostsId &&
                invitation.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (alreadyInvited)
        {
            throw new ConflictException("This freelancer was already invited to this job post.");
        }

        var invitation = new JobInvitation
        {
            JobInvitationsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            ClientProfilesId = clientProfile.ClientProfilesId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            Status = (int)JobInvitationStatus.Pending,
            Message = JobInvitationRules.CleanMessage(command.Request.Message),
            ExpiresAt = command.Request.ExpiresAt,
            CreatedAt = _dateTimeService.UtcNow
        };

        _context.Set<JobInvitation>().Add(invitation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException("This freelancer was already invited to this job post.", exception);
        }

        await NotifyFreelancerAsync(freelancerProfile.UserId, invitation.JobInvitationsId, jobPost.Title, cancellationToken);

        return await _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(item => item.JobInvitationsId == invitation.JobInvitationsId)
            .ProjectToJobInvitationDto()
            .FirstAsync(cancellationToken);
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
