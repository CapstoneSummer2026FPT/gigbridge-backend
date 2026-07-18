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

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public sealed class ResolveAdminDisputeCommandHandler :
    IRequestHandler<ResolveAdminDisputeCommand, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notifications;
    private readonly ILogger<ResolveAdminDisputeCommandHandler> _logger;

    public ResolveAdminDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notifications,
        ILogger<ResolveAdminDisputeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        ResolveAdminDisputeCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(
            _context,
            command.AdminId,
            cancellationToken);

        if (!Enum.IsDefined(command.Resolution))
            throw new BadRequestException("Invalid dispute resolution.");

        if (string.IsNullOrWhiteSpace(command.ResolutionNote))
            throw new BadRequestException("Resolution note is required.");

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.Status != (int)DisputeStatus.UnderReview)
        {
            throw new BadRequestException("Only disputes under review can be resolved.");
        }

        var now = _dateTimeService.UtcNow;
        dispute.Status = (int)DisputeStatus.Resolved;
        dispute.Resolution = (int)command.Resolution;
        dispute.ResolutionNote = command.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = command.AdminId;
        dispute.ResolvedAt = now;
        dispute.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        var resolutionLabel = AdminDisputeSupport.GetResolutionLabel(dispute.Resolution);
        await AdminDisputeSupport.NotifyParticipantsAsync(
            _notifications,
            _logger,
            dispute.Contracts,
            dispute,
            $"The dispute on contract '{dispute.Contracts.Title}' was resolved: {resolutionLabel}.",
            cancellationToken);

        return await AdminDisputeSupport.GetDetailAsync(
            _context,
            dispute.DisputesId,
            cancellationToken);
    }
}
