using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
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
        if (contract.Status == (int)ContractStatus.Disputed)
        {
            throw new BadRequestException("Cannot perform this action while the contract is under dispute.");
        }

        if (contract.Status != (int)ContractStatus.Active)
        {
            throw new BadRequestException("Milestones can only be managed after the contract is active.");
        }
    }

    public static void EnsureNotDisputed(Contract contract)
    {
        if (contract.Status == (int)ContractStatus.Disputed)
        {
            throw new BadRequestException("Cannot perform this action while the contract is under dispute.");
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
        var attachments = milestone.MilestoneAttachments != null
            ? System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(milestone.MilestoneAttachments, a => new MilestoneAttachmentResponse(
                a.MilestoneAttachmentsId,
                a.MilestonesId,
                a.FileName,
                a.FileUrl,
                a.FileSize,
                a.SourceType,
                a.MimeType,
                a.UploadedByUserId,
                a.CreatedAt)))
            : new System.Collections.Generic.List<MilestoneAttachmentResponse>();
        var workItems = milestone.WorkItems != null
            ? milestone.WorkItems.OrderBy(item => item.OrderIndex).Select(item => new ContractWorkItemResponse(
                item.ContractWorkItemId,
                item.MilestonesId,
                item.Title,
                item.Description,
                item.Deliverables,
                item.EstimatedDuration,
                item.OrderIndex,
                item.Status,
                item.ProgressNote,
                item.CompletedAt,
                item.UpdatedAt)).ToList()
            : [];

        return new ContractMilestoneResponse(
            milestone.MilestonesId,
            milestone.ContractsId,
            milestone.Title,
            milestone.Description,
            milestone.Amount,
            milestone.EstimatedDuration,
            milestone.DueDate,
            milestone.Deliverables,
            milestone.AcceptanceCriteria,
            milestone.Status,
            milestone.SortOrder,
            milestone.StartedAt,
            milestone.SubmittedAt,
            milestone.ApprovedAt,
            milestone.ReleasedAmount,
            milestone.LastReleasedAt,
            milestone.SubmissionDescription,
            attachments,
            workItems);
    }

    /// <summary>
    /// The contract's Client + (if assigned) Freelancer user ids — the exact recipient set
    /// for workspace realtime events, never broadcast beyond these two participants.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> GetParticipantUserIdsAsync(
        IApplicationDbContext context,
        Contract contract,
        CancellationToken cancellationToken)
    {
        var userIds = new List<Guid>();
        var clientUserId = await context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (clientUserId != Guid.Empty) userIds.Add(clientUserId);

        if (contract.FreelancerProfilesId.HasValue)
        {
            var freelancerUserId = await context.Set<FreelancerProfile>()
                .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(profile => profile.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (freelancerUserId != Guid.Empty) userIds.Add(freelancerUserId);
        }

        return userIds;
    }

    public static IOrderedQueryable<Milestone> OrderMilestones(IQueryable<Milestone> milestones)
    {
        return milestones
            .OrderBy(milestone => milestone.SortOrder ?? int.MaxValue)
            .ThenBy(milestone => milestone.CreatedAt)
            .ThenBy(milestone => milestone.MilestonesId);
    }

    /// <summary>
    /// Advances the next consecutive Pending milestone to InProgress once every milestone
    /// before it (by SortOrder) is Approved or Completed. No-op if no such milestone exists
    /// (e.g. the contract has no remaining milestones, or the chain is already broken).
    /// </summary>
    public static void AdvanceNextMilestone(IReadOnlyList<Milestone> orderedMilestones, DateTime now)
    {
        var next = orderedMilestones.FirstOrDefault(candidate =>
            candidate.Status == (int)MilestoneStatus.Pending &&
            orderedMilestones.Where(previous => (previous.SortOrder ?? 0) < (candidate.SortOrder ?? 0))
                .All(previous => previous.Status is (int)MilestoneStatus.Approved or (int)MilestoneStatus.Completed));
        if (next is not null)
        {
            next.Status = (int)MilestoneStatus.InProgress;
            next.StartedAt = now;
            next.UpdatedAt = now;
        }
    }
}
