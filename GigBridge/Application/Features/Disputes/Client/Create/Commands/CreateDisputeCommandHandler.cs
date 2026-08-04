using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Premium.Client.FastTrackArbitration.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Client.Create.Commands;

public sealed class CreateDisputeCommandHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IDateTimeService clock) : IRequestHandler<CreateDisputeCommand, DisputeDto>
{
    public async Task<DisputeDto> Handle(CreateDisputeCommand command, CancellationToken cancellationToken)
    {
        var contract = await context.Set<Contract>()
            .Include(x => x.ClientProfiles)
            .FirstOrDefaultAsync(x => x.ContractsId == command.Request.ContractId &&
                x.ClientProfiles.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Contract not found.");
        if (contract.Status is not ((int)ContractStatus.Active) and not ((int)ContractStatus.Completed)
            and not ((int)ContractStatus.Disputed))
            throw new ConflictException("The contract is not eligible for a dispute.");
        if (command.Request.MilestoneId.HasValue)
        {
            var milestoneExists = await context.Set<Milestone>().AsNoTracking()
                .AnyAsync(x => x.MilestonesId == command.Request.MilestoneId.Value &&
                    x.ContractsId == contract.ContractsId, cancellationToken);
            if (!milestoneExists) throw new NotFoundException("Milestone not found.");
        }

        var now = clock.UtcNow;
        var isPremium = await premiumAccess.IsPremiumClientAsync(command.UserId, cancellationToken);
        var fastTrack = FastTrackArbitrationPolicy.Evaluate(isPremium, now);
        var dispute = new Dispute
        {
            DisputesId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            InitiatorId = command.UserId,
            MilestonesId = command.Request.MilestoneId,
            Reason = command.Request.Reason.Trim(),
            Status = 0,
            IsVipPriority = fastTrack.IsVipPriority,
            ResolutionTargetAt = fastTrack.ResolutionTargetAt,
            AiAnalysisStatus = fastTrack.AiAnalysisStatus,
            CreatedAt = now
        };
        contract.Status = (int)ContractStatus.Disputed;
        contract.UpdatedAt = now;
        context.Set<Dispute>().Add(dispute);
        await context.SaveChangesAsync(cancellationToken);
        return Map(dispute);
    }

    private static DisputeDto Map(Dispute x) => new(
        x.DisputesId, x.ContractsId, x.InitiatorId, x.MilestonesId, x.Reason,
        x.Status, x.Resolution, x.ResolutionNote, x.IsVipPriority,
        x.ResolutionTargetAt, x.AiAnalysisStatus.ToString(), x.CreatedAt);
}
