using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Internal;

internal sealed record DisputeParticipants(
    Guid ClientUserId,
    Guid? FreelancerUserId)
{
    public bool Contains(Guid userId) =>
        userId == ClientUserId || FreelancerUserId == userId;

    public string? GetRole(Guid userId)
    {
        if (userId == ClientUserId)
            return "Client";

        return FreelancerUserId == userId ? "Freelancer" : null;
    }

    public Guid? GetOtherParty(Guid userId) =>
        userId == ClientUserId ? FreelancerUserId : ClientUserId;
}

internal static class DisputeAccess
{
    public static readonly int[] ActiveStatuses =
    [
        (int)DisputeStatus.Open,
        (int)DisputeStatus.WaitingAdmin,
        (int)DisputeStatus.UnderReview,
        (int)DisputeStatus.WaitingEvidence,
        (int)DisputeStatus.DecisionPending
    ];

    public static bool CanCreateForContractStatus(int status) =>
        status is (int)ContractStatus.PendingEscrow
            or (int)ContractStatus.Active
            or (int)ContractStatus.Completed;

    public static void EnsureCreationAllowed(Contract contract)
    {
        if (contract.Status == (int)ContractStatus.Disputed)
        {
            throw new BadRequestException("A dispute already exists for this contract.");
        }

        if (!CanCreateForContractStatus(contract.Status))
        {
            throw new BadRequestException(
                "Disputes can only be opened after the contract is fully signed, while it is pending escrow, active, or completed.");
        }
    }

    public static async Task<Contract> GetContractAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        return await context.Set<Contract>()
            .AsNoTracking()
            .FirstOrDefaultAsync(contract => contract.ContractsId == contractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");
    }

    public static async Task<DisputeParticipants> EnsureParticipantAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var clientUserId = await context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        Guid? freelancerUserId = null;
        if (contract.FreelancerProfilesId.HasValue)
        {
            var resolvedFreelancerUserId = await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(profile => profile.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (resolvedFreelancerUserId != Guid.Empty)
                freelancerUserId = resolvedFreelancerUserId;
        }

        var participants = new DisputeParticipants(clientUserId, freelancerUserId);
        if (clientUserId == Guid.Empty || !participants.Contains(userId))
        {
            throw new ForbiddenAccessException(
                "Only the contract client or freelancer can access dispute information.");
        }

        return participants;
    }
}
