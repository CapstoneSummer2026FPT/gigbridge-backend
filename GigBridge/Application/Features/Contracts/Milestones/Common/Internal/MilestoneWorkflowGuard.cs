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

    /// <param name="deliveryMode">
    /// Enum MilestoneDeliveryMode from the owning contract. Passed in rather than read off the
    /// milestone because the milestone has no back-pointer loaded on most of these code paths;
    /// callers that genuinely have no contract in hand fall through to Legacy, which is the safe
    /// default (it routes the client to the existing milestone-level screens).
    /// </param>
    public static ContractMilestoneResponse ToResponse(Milestone milestone, int deliveryMode = 0)
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
                item.DueDate,
                item.OrderIndex,
                item.Status,
                item.ProgressNote,
                item.CompletedAt,
                item.UpdatedAt,
                ToSubmissionResponses(item))).ToList()
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
            workItems,
            deliveryMode);
    }

    private static IReadOnlyList<ContractWorkItemSubmissionResponse> ToSubmissionResponses(ContractWorkItem item)
    {
        if (item.Submissions is null || item.Submissions.Count == 0)
        {
            return [];
        }

        return item.Submissions
            .OrderBy(submission => submission.RevisionNumber)
            .Select(submission => new ContractWorkItemSubmissionResponse(
                submission.ContractWorkItemSubmissionId,
                submission.ContractWorkItemId,
                submission.RevisionNumber,
                submission.Note,
                submission.SubmittedAt,
                submission.SubmittedByUserId,
                submission.ReviewStatus,
                submission.ReviewedAt,
                submission.ReviewedByUserId,
                submission.ReviewReason,
                submission.Attachments is null
                    ? []
                    : submission.Attachments
                        .OrderBy(attachment => attachment.CreatedAt)
                        .Select(attachment => new MilestoneAttachmentResponse(
                            attachment.MilestoneAttachmentsId,
                            attachment.MilestonesId,
                            attachment.FileName,
                            attachment.FileUrl,
                            attachment.FileSize,
                            attachment.SourceType,
                            attachment.MimeType,
                            attachment.UploadedByUserId,
                            attachment.CreatedAt))
                        .ToList()))
            .ToList();
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

    /// <summary>
    /// The contract's Client user id, full name, and email — used to address notifications
    /// (e.g. the milestone submission email) to the JobPost owner.
    /// </summary>
    public static async Task<(Guid UserId, string FullName, string Email)> GetClientContactAsync(
        IApplicationDbContext context,
        Contract contract,
        CancellationToken cancellationToken)
    {
        var clientUserId = await context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (clientUserId == Guid.Empty)
        {
            throw new NotFoundException("Client account does not exist.");
        }

        var user = await context.Set<User>()
            .Where(user => user.UserId == clientUserId)
            .Select(user => new { user.FullName, user.Email })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? throw new NotFoundException("Client account does not exist.")
            : (clientUserId, user.FullName, user.Email);
    }

    /// <summary>
    /// The freelancer's contact details for delivery emails. Returns null when the contract has no
    /// freelancer assigned yet, which is a legitimate state before an offer is accepted — callers
    /// simply skip the email rather than failing the operation that already committed.
    /// </summary>
    public static async Task<(Guid UserId, string FullName, string Email)?> GetFreelancerContactAsync(
        IApplicationDbContext context,
        Contract contract,
        CancellationToken cancellationToken)
    {
        if (contract.FreelancerProfilesId is null)
        {
            return null;
        }

        var freelancerUserId = await context.Set<FreelancerProfile>()
            .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (freelancerUserId == Guid.Empty)
        {
            return null;
        }

        var user = await context.Set<User>()
            .Where(user => user.UserId == freelancerUserId)
            .Select(user => new { user.FullName, user.Email })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : (freelancerUserId, user.FullName, user.Email);
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
    public static Milestone? AdvanceNextMilestone(IReadOnlyList<Milestone> orderedMilestones, DateTime now)
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

        return next;
    }

    /// <summary>
    /// True if a Pending milestone is allowed to transition to InProgress right now: every
    /// milestone before it (by SortOrder) is Approved or Completed, or the freelancer holds an
    /// Approved early-start request for this specific milestone. This is the single source of
    /// truth for "can this milestone start" shared by UpdateContractWorkItemCommandHandler
    /// (freelancer starting via work item update) and RespondMilestoneEarlyStartCommandHandler
    /// (client approving early start).
    /// </summary>
    public static bool IsEligibleToStart(
        Milestone candidate,
        IReadOnlyList<Milestone> orderedMilestones,
        bool hasApprovedEarlyStartRequest)
    {
        var allPriorApproved = orderedMilestones
            .Where(previous => (previous.SortOrder ?? 0) < (candidate.SortOrder ?? 0))
            .All(previous => previous.Status is (int)MilestoneStatus.Approved or (int)MilestoneStatus.Completed);

        return allPriorApproved || hasApprovedEarlyStartRequest;
    }
}
