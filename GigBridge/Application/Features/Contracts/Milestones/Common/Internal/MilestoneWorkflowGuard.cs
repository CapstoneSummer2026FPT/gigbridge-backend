using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.Internal;

internal enum ContractMilestoneParticipantRole
{
    Client,
    Freelancer
}

internal static class MilestoneWorkflowGuard
{
    public static async Task<Contract> GetContractAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var contract = await context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == contractId, cancellationToken);

        return contract ?? throw new NotFoundException("Contract does not exist.");
    }

    public static async Task<Milestone> GetMilestoneAsync(
        IApplicationDbContext context,
        Guid contractId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var milestone = await context.Set<Milestone>()
            .FirstOrDefaultAsync(
                milestone =>
                    milestone.ContractsId == contractId &&
                    milestone.MilestonesId == milestoneId,
                cancellationToken);

        return milestone ?? throw new NotFoundException("Milestone does not exist.");
    }

    public static void EnsureContractActive(Contract contract)
    {
        if (contract.Status != (int)ContractStatus.Active)
        {
            throw new BadRequestException("Milestones can only be managed after the contract is active.");
        }
    }

    public static async Task EnsureClientAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await context.Set<ClientProfile>()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (!isClient)
        {
            throw new ForbiddenAccessException("Only the owning client can perform this milestone action.");
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
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken);

        if (!isFreelancer)
        {
            throw new ForbiddenAccessException("Only the selected freelancer can perform this milestone action.");
        }
    }

    public static async Task<ContractMilestoneParticipantRole> EnsureParticipantAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await context.Set<ClientProfile>()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isClient)
        {
            return ContractMilestoneParticipantRole.Client;
        }

        if (contract.FreelancerProfilesId.HasValue)
        {
            var isFreelancer = await context.Set<FreelancerProfile>()
                .AnyAsync(
                    profile =>
                        profile.UserId == userId &&
                        profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

            if (isFreelancer)
            {
                return ContractMilestoneParticipantRole.Freelancer;
            }
        }

        throw new ForbiddenAccessException("Only contract participants can view milestones.");
    }

    public static ContractMilestoneResponse ToResponse(Milestone milestone)
    {
        return new ContractMilestoneResponse(
            milestone.MilestonesId,
            milestone.ContractsId,
            milestone.Title,
            milestone.Amount,
            milestone.DueDate,
            milestone.Status,
            milestone.SortOrder,
            milestone.StartedAt,
            milestone.SubmittedAt,
            milestone.ApprovedAt,
            milestone.ReleasedAmount,
            milestone.LastReleasedAt);
    }
}
