using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using Domain.Enums.JobInvitations;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobInvitations.Freelancer.ApplyInvitation.Commands;

public sealed class ApplyJobInvitationCommandHandler
    : IRequestHandler<ApplyJobInvitationCommand, JobInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApplyJobInvitationCommandHandler> _logger;

    public ApplyJobInvitationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        ILogger<ApplyJobInvitationCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<JobInvitationDto> Handle(
        ApplyJobInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await JobInvitationRules.GetFreelancerProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var invitation = await JobInvitationRules.GetOwnedReceivedInvitationAsync(
            _context,
            command.InvitationId,
            freelancerProfile.FreelancerProfilesId,
            cancellationToken);

        JobInvitationRules.EnsureCanRespond(invitation);

        var now = _dateTimeService.UtcNow;
        invitation.Status = (int)JobInvitationStatus.Applied;
        invitation.ViewedAt ??= now;
        invitation.RespondedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = await LoadDtoAsync(invitation.JobInvitationsId, cancellationToken);
        await NotifyClientAsync(dto.ClientUserId, dto.JobInvitationId, dto.JobTitle, dto.FreelancerName, cancellationToken);

        return dto;
    }

    private async Task<JobInvitationDto> LoadDtoAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        return await _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(invitation => invitation.JobInvitationsId == invitationId)
            .ProjectToJobInvitationDto()
            .FirstAsync(cancellationToken);
    }

    private async Task NotifyClientAsync(
        Guid clientUserId,
        Guid invitationId,
        string jobTitle,
        string? freelancerName,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.CreateNotificationAsync(
                clientUserId,
                NotificationType.SystemAlert,
                "Invitation accepted",
                $"{freelancerName ?? "A freelancer"} accepted your invitation for \"{jobTitle}\".",
                invitationId,
                "JobInvitation",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send job invitation accepted notification {InvitationId}.", invitationId);
        }
    }
}
