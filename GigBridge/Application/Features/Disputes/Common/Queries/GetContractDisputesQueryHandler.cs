using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetContractDisputesQueryHandler :
    IRequestHandler<GetContractDisputesQuery, IReadOnlyList<DisputeResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetContractDisputesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DisputeResponse>> Handle(
        GetContractDisputesQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == query.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        await EnsureParticipantAsync(contract, query.UserId, cancellationToken);

        var disputes = await _context.Set<Dispute>()
            .Where(d => d.ContractsId == query.ContractId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        if (disputes.Count == 0)
        {
            return Array.Empty<DisputeResponse>();
        }

        // Preload initiator names
        var initiatorIds = disputes
            .Select(d => d.InitiatorId)
            .Distinct()
            .ToHashSet();

        var users = await _context.Set<User>()
            .Where(u => initiatorIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        // Preload milestone titles for milestone-linked disputes
        var milestoneIds = disputes
            .Where(d => d.MilestonesId.HasValue)
            .Select(d => d.MilestonesId!.Value)
            .Distinct()
            .ToHashSet();

        var milestones = milestoneIds.Count > 0
            ? await _context.Set<Milestone>()
                .Where(m => milestoneIds.Contains(m.MilestonesId))
                .ToDictionaryAsync(m => m.MilestonesId, m => m.Title, cancellationToken)
            : new Dictionary<Guid, string>();

        // Preload evidence grouped by dispute
        var disputeIds = disputes.Select(d => d.DisputesId).ToHashSet();
        var allEvidences = await _context.Set<DisputeEvidence>()
            .Where(e => disputeIds.Contains(e.DisputesId))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var evidencesByDispute = allEvidences
            .GroupBy(e => e.DisputesId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Preload client and freelancer userIds for role resolution
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

        return disputes.Select(dispute =>
        {
            var initiatorName = users.GetValueOrDefault(dispute.InitiatorId);
            var initiatorRole = ResolveInitiatorRole(dispute.InitiatorId, clientUserId, freelancerUserId);
            var milestoneTitle = dispute.MilestonesId.HasValue
                ? milestones.GetValueOrDefault(dispute.MilestonesId.Value)
                : null;
            var evList = evidencesByDispute.GetValueOrDefault(dispute.DisputesId) ?? new List<DisputeEvidence>();

            return new DisputeResponse(
                dispute.DisputesId,
                dispute.ContractsId,
                dispute.InitiatorId,
                initiatorName,
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
                evList.Select(e => new DisputeEvidenceResponse(
                    e.DisputeEvidenceId,
                    e.DisputesId,
                    e.UploadedById,
                    e.FileName,
                    e.FileUrl,
                    e.FileSize,
                    e.Description,
                    e.CreatedAt)).ToList());
        }).ToList();
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
        // Check if user is the client
        var isClient = await _context.Set<ClientProfile>()
            .AnyAsync(p =>
                p.UserId == userId &&
                p.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isClient)
            return;

        // Check if user is the freelancer
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

        throw new ForbiddenAccessException("Only the contract client or freelancer can view disputes.");
    }
}
