using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.UpdateStatus.Commands;

public sealed class UpdateAdminDisputeStatusCommandHandler :
    IRequestHandler<UpdateAdminDisputeStatusCommand, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateAdminDisputeStatusCommandHandler> _logger;

    public UpdateAdminDisputeStatusCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notifications,
        ILogger<UpdateAdminDisputeStatusCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        UpdateAdminDisputeStatusCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(
            _context,
            command.AdminId,
            cancellationToken);

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var isValidTransition =
            dispute.Status == (int)DisputeStatus.Open &&
            command.Status == DisputeStatus.UnderReview ||
            dispute.Status == (int)DisputeStatus.Resolved &&
            command.Status == DisputeStatus.Closed;

        if (!isValidTransition)
        {
            throw new BadRequestException(
                "Invalid dispute status transition. Allowed transitions are Open to UnderReview and Resolved to Closed.");
        }

        dispute.Status = (int)command.Status;
        dispute.UpdatedAt = _dateTimeService.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var statusLabel = command.Status == DisputeStatus.UnderReview ? "under review" : "closed";
        await AdminDisputeSupport.NotifyParticipantsAsync(
            _notifications,
            _logger,
            dispute.Contracts,
            dispute,
            $"The dispute on contract '{dispute.Contracts.Title}' is now {statusLabel}.",
            cancellationToken);

        return await AdminDisputeSupport.GetDetailAsync(
            _context,
            dispute.DisputesId,
            cancellationToken);
    }
}
