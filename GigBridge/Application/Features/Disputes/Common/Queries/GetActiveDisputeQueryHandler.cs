using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetActiveDisputeQueryHandler :
    IRequestHandler<GetActiveDisputeQuery, DisputeResponse?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveDisputeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeResponse?> Handle(
        GetActiveDisputeQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == query.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        await EnsureParticipantAsync(contract, query.UserId, cancellationToken);

        var activeDispute = await _context.Set<Dispute>()
            .Where(d =>
                d.ContractsId == query.ContractId &&
                (d.Status == (int)DisputeStatus.Open ||
                 d.Status == (int)DisputeStatus.UnderReview))
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeDispute is null)
        {
            return null;
        }

        // Load initiator info
        var initiator = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.UserId == activeDispute.InitiatorId, cancellationToken);

        // Load milestone title if applicable
        string? milestoneTitle = null;
        if (activeDispute.MilestonesId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .Where(m => m.MilestonesId == activeDispute.MilestonesId.Value)
                .Select(m => m.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Load evidences
        var evidences = await _context.Set<DisputeEvidence>()
            .Where(e => e.DisputesId == activeDispute.DisputesId)
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

        var initiatorRole = ResolveInitiatorRole(activeDispute.InitiatorId, clientUserId, freelancerUserId);

        return new DisputeResponse(
            activeDispute.DisputesId,
            activeDispute.ContractsId,
            activeDispute.InitiatorId,
            initiator?.FullName,
            initiatorRole,
            activeDispute.MilestonesId,
            milestoneTitle,
            activeDispute.Reason,
            activeDispute.Status,
            activeDispute.Resolution,
            null, // ResolutionLabel — not resolved yet
            activeDispute.ResolutionNote,
            activeDispute.ResolvedAt,
            activeDispute.CreatedAt,
            activeDispute.UpdatedAt,
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

        throw new ForbiddenAccessException("Only the contract client or freelancer can view dispute information.");
    }
}
