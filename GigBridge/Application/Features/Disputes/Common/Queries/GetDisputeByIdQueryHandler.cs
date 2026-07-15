using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetDisputeByIdQueryHandler :
    IRequestHandler<GetDisputeByIdQuery, DisputeResponse>
{
    private readonly IApplicationDbContext _context;

    public GetDisputeByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeResponse> Handle(
        GetDisputeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var dispute = await _context.Set<Dispute>()
            .FirstOrDefaultAsync(d => d.DisputesId == query.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.ContractsId != query.ContractId)
        {
            throw new BadRequestException("The specified dispute does not belong to this contract.");
        }

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == query.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        await EnsureParticipantAsync(contract, query.UserId, cancellationToken);

        // Load initiator
        var initiator = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.UserId == dispute.InitiatorId, cancellationToken);

        // Load milestone title if applicable
        string? milestoneTitle = null;
        if (dispute.MilestonesId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .Where(m => m.MilestonesId == dispute.MilestonesId.Value)
                .Select(m => m.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Load evidences
        var evidences = await _context.Set<DisputeEvidence>()
            .Where(e => e.DisputesId == dispute.DisputesId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Resolve client and freelancer userIds for role
        var clientUserId = await _context.Set<ClientProfile>()
            .Where(p => p.ClientProfilesId == contract.ClientProfilesId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        Guid? freelancerUserId = null;
        if (contract.FreelancerProfilesId.HasValue)
        {
            freelancerUserId = await _context.Set<FreelancerProfile>()
                .Where(p => p.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var resolutionLabels = new Dictionary<int?, string?>
        {
            { (int)DisputeResolution.ClientFavored, "Client Favored" },
            { (int)DisputeResolution.FreelancerFavored, "Freelancer Favored" },
            { (int)DisputeResolution.Split, "Split" },
            { (int)DisputeResolution.Dismissed, "Dismissed" }
        };

        var initiatorRole = ResolveInitiatorRole(dispute.InitiatorId, clientUserId, freelancerUserId);

        return new DisputeResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.InitiatorId,
            initiator?.FullName,
            initiatorRole,
            dispute.MilestonesId,
            milestoneTitle,
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            dispute.Resolution.HasValue ? resolutionLabels.GetValueOrDefault(dispute.Resolution) : null,
            dispute.ResolutionNote,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            evidences.Select(e => new DisputeEvidenceResponse(
                e.DisputeEvidenceId,
                e.DisputesId,
                e.UploadedById,
                e.FileName,
                e.FileUrl,
                e.FileSize,
                e.Description,
                e.CreatedAt)).ToList());
    }

    private static string? ResolveInitiatorRole(Guid userId, Guid clientUserId, Guid? freelancerUserId)
    {
        if (userId == clientUserId)
            return "Client";
        if (freelancerUserId.HasValue && userId == freelancerUserId.Value)
            return "Freelancer";
        return null;
    }

    private async Task EnsureParticipantAsync(
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await _context.Set<ClientProfile>()
            .AnyAsync(p =>
                p.UserId == userId &&
                p.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isClient)
            return;

        if (contract.FreelancerProfilesId.HasValue)
        {
            var isFreelancer = await _context.Set<FreelancerProfile>()
                .AnyAsync(p =>
                    p.UserId == userId &&
                    p.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

            if (isFreelancer)
                return;
        }

        throw new ForbiddenAccessException("Only the contract client or freelancer can view dispute details.");
    }
}
