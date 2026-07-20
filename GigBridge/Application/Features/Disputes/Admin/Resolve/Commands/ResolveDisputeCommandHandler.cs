using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Admin.Resolve.Commands;

public sealed class ResolveDisputeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock) : IRequestHandler<ResolveDisputeCommand, AdminDisputeDto>
{
    public async Task<AdminDisputeDto> Handle(
        ResolveDisputeCommand command,
        CancellationToken cancellationToken)
    {
        var dispute = await context.Set<Dispute>()
            .FirstOrDefaultAsync(x => x.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute not found.");
        if (dispute.Status >= 2) throw new ConflictException("The dispute is already resolved.");
        dispute.Status = 2;
        dispute.Resolution = command.Request.Resolution;
        dispute.ResolutionNote = command.Request.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = command.AdminUserId;
        dispute.ResolvedAt = clock.UtcNow;
        dispute.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return new AdminDisputeDto(
            dispute.DisputesId, dispute.ContractsId, dispute.InitiatorId,
            dispute.MilestonesId, dispute.Reason, dispute.Status, dispute.Resolution,
            dispute.ResolutionNote, dispute.IsVipPriority, dispute.ResolutionTargetAt,
            dispute.AiAnalysisStatus.ToString(), dispute.AiSuggestedResolution,
            dispute.CreatedAt, dispute.ResolvedAt, dispute.ResolvedByAdminId);
    }
}
