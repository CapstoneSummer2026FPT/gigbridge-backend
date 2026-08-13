using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.Common;

internal static class ContractProductHandoffAccess
{
    public static async Task<Contract> GetActiveContractAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var contract = await context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == contractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.Active)
        {
            throw new BadRequestException("Product handoff is only available for active contracts.");
        }

        return contract;
    }

    public static async Task EnsureParticipantAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile => profile.UserId == userId && profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isClient)
        {
            return;
        }

        var isFreelancer = contract.FreelancerProfilesId.HasValue &&
            await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .AnyAsync(
                    profile =>
                        profile.UserId == userId &&
                        profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

        if (isFreelancer)
        {
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access this product handoff.");
    }

    public static async Task EnsureClientAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile => profile.UserId == userId && profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (!isClient)
        {
            throw new ForbiddenAccessException("Only the owning client can send product materials.");
        }
    }

    public static async Task EnsureFreelancerAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!contract.FreelancerProfilesId.HasValue)
        {
            throw new BadRequestException("Contract does not have a selected freelancer.");
        }

        var isFreelancer = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken);

        if (!isFreelancer)
        {
            throw new ForbiddenAccessException("Only the selected freelancer can acknowledge product materials.");
        }
    }

    public static async Task<IReadOnlyList<Guid>> GetParticipantUserIdsAsync(
        IApplicationDbContext context,
        Contract contract,
        CancellationToken cancellationToken)
    {
        var userIds = new List<Guid>();

        var clientUserId = await context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId.HasValue)
        {
            userIds.Add(clientUserId.Value);
        }

        if (contract.FreelancerProfilesId.HasValue)
        {
            var freelancerUserId = await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(profile => (Guid?)profile.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (freelancerUserId.HasValue)
            {
                userIds.Add(freelancerUserId.Value);
            }
        }

        return userIds.Distinct().ToList();
    }

    public static Task<Guid?> GetWorkroomConversationIdAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        return context.Set<Conversation>()
            .AsNoTracking()
            .Where(conversation => conversation.ContractsId == contractId)
            .OrderByDescending(conversation => conversation.ConversationType == (int)ConversationType.ContractWorkroom)
            .ThenByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .Select(conversation => (Guid?)conversation.ConversationsId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
