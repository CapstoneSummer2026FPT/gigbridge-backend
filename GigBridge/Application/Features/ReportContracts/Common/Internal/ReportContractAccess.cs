using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportContracts.Common.Internal;

internal sealed record ReportContractParticipants(
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

    public Guid? GetUserIdByRole(string role) =>
        role == "Client" ? ClientUserId : FreelancerUserId;
}

internal static class ReportContractAccess
{
    public static async Task<Contract> GetContractAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        return await context.Set<Contract>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContractsId == contractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");
    }

    public static async Task<ReportContractParticipants> EnsureParticipantAsync(
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

        var participants = new ReportContractParticipants(clientUserId, freelancerUserId);
        if (clientUserId == Guid.Empty || !participants.Contains(userId))
        {
            throw new ForbiddenAccessException(
                "Only the contract client or freelancer can access contract report information.");
        }

        return participants;
    }
}
