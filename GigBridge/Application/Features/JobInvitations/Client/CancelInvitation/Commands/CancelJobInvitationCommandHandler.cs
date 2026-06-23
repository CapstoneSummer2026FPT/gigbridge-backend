using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.JobInvitations.Client.CancelInvitation.Commands;

public sealed class CancelJobInvitationCommandHandler
    : IRequestHandler<CancelJobInvitationCommand, JobInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CancelJobInvitationCommandHandler> _logger;

    public CancelJobInvitationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        ILogger<CancelJobInvitationCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<JobInvitationDto> Handle(
        CancelJobInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var invitation = await JobInvitationRules.GetOwnedSentInvitationAsync(
            _context,
            command.InvitationId,
            clientProfile.ClientProfilesId,
            cancellationToken);

        JobInvitationRules.EnsureCanRespond(invitation);

        var now = _dateTimeService.UtcNow;
        invitation.Status = (int)JobInvitationStatus.Cancelled;
        invitation.RespondedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = await LoadDtoAsync(invitation.JobInvitationsId, cancellationToken);
        await NotifyFreelancerAsync(dto.FreelancerUserId, dto.JobInvitationId, dto.JobTitle, cancellationToken);

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
                "Job invitation cancelled",
                $"The invitation for \"{jobTitle}\" was cancelled.",
                invitationId,
                "JobInvitation",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send job invitation cancellation notification {InvitationId}.", invitationId);
        }
    }
}
