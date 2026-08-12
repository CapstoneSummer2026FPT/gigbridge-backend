using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using Domain.Enums.JobInvitations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobInvitations.Freelancer.ViewInvitation.Commands;

public sealed class ViewJobInvitationCommandHandler
    : IRequestHandler<ViewJobInvitationCommand, JobInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ViewJobInvitationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<JobInvitationDto> Handle(
        ViewJobInvitationCommand command,
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

        if (invitation.Status == (int)JobInvitationStatus.Pending)
        {
            invitation.Status = (int)JobInvitationStatus.Viewed;
            invitation.ViewedAt = _dateTimeService.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(item => item.JobInvitationsId == invitation.JobInvitationsId)
            .ProjectToJobInvitationDto()
            .FirstAsync(cancellationToken);
    }
}
