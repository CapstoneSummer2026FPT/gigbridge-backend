using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.Common.Internal;

internal static class AdminDisputeSupport
{
    private static readonly Dictionary<int, string> ResolutionLabels = new()
    {
        [(int)DisputeResolution.ClientFavored] = "Client Favored",
        [(int)DisputeResolution.FreelancerFavored] = "Freelancer Favored",
        [(int)DisputeResolution.Split] = "Split",
        [(int)DisputeResolution.Dismissed] = "Dismissed"
    };

    public static string? GetResolutionLabel(int? resolution) =>
        resolution.HasValue ? ResolutionLabels.GetValueOrDefault(resolution.Value) : null;

    public static async Task EnsureAdminAsync(
        IApplicationDbContext context,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var isAdmin = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                    user.UserId == adminId &&
                    user.Role == (int)UserRole.Admin &&
                    user.IsActive,
                cancellationToken);

        if (!isAdmin)
            throw new ForbiddenAccessException("An active administrator account is required.");
    }

    public static async Task<AdminDisputeDetailResponse> GetDetailAsync(
        IApplicationDbContext context,
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var dispute = await context.Set<Dispute>()
            .AsNoTracking()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
                    .ThenInclude(profile => profile.User)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
                    .ThenInclude(profile => profile!.User)
            .Include(item => item.Initiator)
            .Include(item => item.Respondent)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.RelatedReport)
            .Include(item => item.Milestones)
            .FirstOrDefaultAsync(item => item.DisputesId == disputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var contract = dispute.Contracts;
        var job = await context.Set<JobPost>()
            .AsNoTracking()
            .Include(item => item.MajorCategory).ThenInclude(item => item!.Major)
            .Include(item => item.MajorCategory).ThenInclude(item => item!.Category)
            .Include(item => item.JobPostSkills).ThenInclude(item => item.Skills)
            .Include(item => item.JobPostQuestions)
            .FirstAsync(item => item.JobPostsId == contract.JobPostsId, cancellationToken);

        var proposal = contract.ProposalsId.HasValue
            ? await context.Set<Proposal>()
                .AsNoTracking()
                .Include(item => item.ProposalAnswers).ThenInclude(item => item.JobPostQuestions)
                .Include(item => item.ProposalMilestonePlans)
                .FirstOrDefaultAsync(item => item.ProposalsId == contract.ProposalsId.Value, cancellationToken)
            : null;

        var milestones = await context.Set<Milestone>()
            .AsNoTracking()
            .Include(item => item.MilestoneAttachments)
            .Where(item => item.ContractsId == contract.ContractsId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var escrow = await context.Set<ContractEscrow>()
            .AsNoTracking()
            .Include(item => item.EscrowTransactions)
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken);
        var serviceFees = await context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(item => item.ContractsId == contract.ContractsId &&
                           item.Status == (int)WalletTransactionStatus.Succeeded &&
                           item.Type == (int)WalletTransactionType.Adjustment &&
                           item.Metadata != null && item.Metadata.Contains("ServiceFee"))
            .SumAsync(item => item.TokenAmount, cancellationToken);

        var conversations = await context.Set<Conversation>()
            .AsNoTracking()
            .Where(item => item.ContractsId == contract.ContractsId)
            .Select(item => new { item.ConversationsId, item.ConversationType, item.DisputesId })
            .ToListAsync(cancellationToken);

        var evidences = await context.Set<DisputeEvidence>()
            .AsNoTracking()
            .Include(item => item.UploadedBy)
            .Include(item => item.RequestedByAdmin)
            .Include(item => item.ReviewedByAdmin)
            .Where(item => item.DisputesId == dispute.DisputesId)
            .OrderBy(item => item.RequestedAt ?? item.CreatedAt)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var auditTrail = await context.Set<AdminAuditLog>()
            .AsNoTracking()
            .Where(item => item.EntityId == dispute.DisputesId && item.EntityType == nameof(Dispute))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new AdminAuditEventResponse(
                item.AdminAuditLogsId,
                item.AdminId,
                item.Action,
                item.OldValues,
                item.NewValues,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var decisions = await context.Set<DisputeMilestoneDecision>()
            .AsNoTracking()
            .Where(item => item.DisputesId == dispute.DisputesId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AdminMilestoneDecisionResponse(
                item.DisputeMilestoneDecisionId,
                item.MilestonesId,
                item.Outcome,
                item.MilestoneAmountSnapshot,
                item.ReleasedAmountSnapshot,
                item.AdditionalReleaseAmount,
                item.RefundAmount,
                item.PenaltyAmount,
                item.Reason,
                item.DecidedByAdminId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var penalties = await context.Set<DisputePenalty>()
            .AsNoTracking()
            .Where(item => item.DisputeId == dispute.DisputesId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AdminDisputePenaltyResponse(
                item.DisputePenaltyId, item.MilestoneId, item.ViolatingUserId,
                item.Amount, item.Reason, item.WalletTransactionId,
                item.EscrowTransactionId, item.Status, item.CreatedAt))
            .ToListAsync(cancellationToken);

        var clientProfile = dispute.Contracts.ClientProfiles;
        var freelancerProfile = dispute.Contracts.FreelancerProfiles;
        var client = new AdminDisputePartyResponse(
            clientProfile.UserId,
            clientProfile.ClientProfilesId,
            clientProfile.User.FullName,
            clientProfile.User.Email,
            clientProfile.User.ViolationCount,
            clientProfile.User.IsFlagged,
            clientProfile.User.AccountStatus,
            clientProfile.User.SuspendedUntil,
            clientProfile.User.BannedAt);
        var freelancer = freelancerProfile is null
            ? null
            : new AdminDisputePartyResponse(
                freelancerProfile.UserId,
                freelancerProfile.FreelancerProfilesId,
                freelancerProfile.User.FullName,
                freelancerProfile.User.Email,
                freelancerProfile.User.ViolationCount,
                freelancerProfile.User.IsFlagged,
                freelancerProfile.User.AccountStatus,
                freelancerProfile.User.SuspendedUntil,
                freelancerProfile.User.BannedAt);

        var initiatorRole = dispute.InitiatorId == client.UserId
            ? "Client"
            : freelancer?.UserId == dispute.InitiatorId ? "Freelancer" : null;

        var approvedCount = milestones.Count(item => item.Status == (int)MilestoneStatus.Approved);
        var progress = milestones.Count == 0
            ? 0
            : (int)Math.Round(approvedCount * 100m / milestones.Count, MidpointRounding.AwayFromZero);
        var proposalAnswers = proposal?.ProposalAnswers
            .ToDictionary(item => item.JobPostQuestionsId, item => item.AnswerText)
            ?? new Dictionary<Guid, string>();
        var refundedAmount = escrow?.EscrowTransactions
            .Where(item => item.Type == (int)EscrowTransactionType.RefundToClient &&
                           item.Status == (int)EscrowTransactionStatus.Succeeded)
            .Sum(item => item.Amount) ?? 0m;
        var penaltyAmount = escrow?.EscrowTransactions
            .Where(item => item.Type == (int)EscrowTransactionType.DisputePenalty &&
                           item.Status == (int)EscrowTransactionStatus.Succeeded)
            .Sum(item => item.Amount) ?? 0m;
        var refundsByMilestone = escrow?.EscrowTransactions
            .Where(item => item.MilestonesId.HasValue &&
                           item.Type == (int)EscrowTransactionType.RefundToClient &&
                           item.Status == (int)EscrowTransactionStatus.Succeeded)
            .GroupBy(item => item.MilestonesId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount))
            ?? new Dictionary<Guid, decimal>();
        var penaltiesByMilestone = escrow?.EscrowTransactions
            .Where(item => item.MilestonesId.HasValue &&
                           item.Type == (int)EscrowTransactionType.DisputePenalty &&
                           item.Status == (int)EscrowTransactionStatus.Succeeded)
            .GroupBy(item => item.MilestonesId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount))
            ?? new Dictionary<Guid, decimal>();
        var remainingAmount = escrow is null
            ? 0m
            : Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);

        return new AdminDisputeDetailResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.Contracts.Title,
            dispute.Contracts.Status,
            dispute.InitiatorId,
            dispute.Initiator.FullName,
            initiatorRole,
            client,
            freelancer,
            dispute.MilestonesId,
            dispute.Milestones?.Title,
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            GetResolutionLabel(dispute.Resolution),
            dispute.ResolutionNote,
            dispute.ResolvedByAdminId,
            dispute.AssignedAdminId,
            dispute.AssignedAt,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            evidences
                .Select(evidence => new DisputeEvidenceResponse(
                    evidence.DisputeEvidenceId,
                    evidence.UploadedById,
                    evidence.FileName,
                    evidence.FileSize,
                    evidence.Description,
                    evidence.CreatedAt,
                    evidence.IsRequestedByAdmin,
                    evidence.RequestGroupId,
                    evidence.RequestedByAdminId,
                    evidence.RequestedAt,
                    evidence.Deadline,
                    evidence.RequestTarget,
                    evidence.IsRequestFulfilled,
                    evidence.ReviewedByAdminId,
                    evidence.ReviewedAt,
                    evidence.ReviewNote,
                    evidence.UploadedBy?.FullName,
                    evidence.RequestedByAdmin?.FullName,
                    evidence.ReviewedByAdmin?.FullName))
                .ToList(),
            dispute.Title,
            dispute.Description,
            dispute.ClaimedAmount,
            dispute.RequestedResolution,
            dispute.Urgency,
            dispute.RespondentId,
            dispute.Respondent?.FullName,
            dispute.AssignedAdmin?.FullName,
            dispute.RelatedReport is null
                ? null
                : new AdminRelatedReportResponse(
                    dispute.RelatedReport.ReportContractId,
                    dispute.RelatedReport.IssueType,
                    dispute.RelatedReport.Description,
                    dispute.RelatedReport.DesiredResolution,
                    dispute.RelatedReport.Status,
                    dispute.RelatedReport.CreatedAt),
            new AdminContractSummaryResponse(
                contract.TotalBudget,
                contract.CreatedAt,
                contract.StartDate,
                contract.EndDate,
                contract.CompletedAt,
                progress),
            new AdminOriginalJobResponse(
                job.JobPostsId,
                job.Title,
                job.Description,
                job.BudgetMin,
                job.BudgetMax,
                job.Currency,
                job.EstimatedDuration,
                job.MajorCategory is null
                    ? null
                    : $"{job.MajorCategory.Major.Name} / {job.MajorCategory.Category.Name}",
                job.JobPostSkills.Select(item => item.Skills.Name)
                    .Concat(job.CustomSkillNames)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList(),
                proposal?.ProposedBudget,
                proposal?.ProposedDuration,
                job.JobPostQuestions.OrderBy(item => item.OrderIndex)
                    .Select(item => new AdminJobQuestionResponse(
                        item.QuestionText,
                        proposalAnswers.GetValueOrDefault(item.JobPostQuestionsId)))
                    .ToList(),
                proposal?.ProposalMilestonePlans.OrderBy(item => item.OrderIndex)
                    .Select(item => new AdminProposalMilestoneResponse(
                        item.Title,
                        item.Description,
                        item.Amount,
                        item.EstimatedDuration,
                        item.Deliverables,
                        item.AcceptanceCriteria,
                        item.OrderIndex))
                    .ToList() ?? []),
            milestones.Select(item => new AdminMilestoneResponse(
                    item.MilestonesId,
                    item.Title,
                    item.Description,
                    item.Amount,
                    item.ReleasedAmount,
                    Math.Max(0m, item.Amount - item.ReleasedAmount -
                        refundsByMilestone.GetValueOrDefault(item.MilestonesId) -
                        penaltiesByMilestone.GetValueOrDefault(item.MilestonesId)),
                    refundsByMilestone.GetValueOrDefault(item.MilestonesId),
                    penaltiesByMilestone.GetValueOrDefault(item.MilestonesId),
                    Math.Max(0m, item.Amount - item.ReleasedAmount -
                        refundsByMilestone.GetValueOrDefault(item.MilestonesId) -
                        penaltiesByMilestone.GetValueOrDefault(item.MilestonesId)),
                    item.Status == (int)MilestoneStatus.Disputed || item.MilestonesId == dispute.MilestonesId,
                    item.Status,
                    item.Deliverables,
                    item.SubmissionDescription,
                    item.DueDate,
                    item.StartedAt,
                    item.SubmittedAt,
                    item.ApprovedAt,
                    item.PaidAt,
                    item.MilestoneAttachments.OrderBy(attachment => attachment.CreatedAt)
                        .Select(attachment => new AdminMilestoneAttachmentResponse(
                            attachment.MilestoneAttachmentsId,
                            attachment.FileName,
                            attachment.FileUrl,
                            attachment.FileSize,
                            attachment.MimeType,
                            attachment.UploadedByUserId,
                            attachment.CreatedAt))
                        .ToList()))
                .ToList(),
            new AdminEscrowSummaryResponse(
                escrow?.ContractEscrowId,
                escrow?.RequiredAmount ?? contract.TotalBudget,
                escrow?.FundedAmount ?? 0m,
                escrow?.ReleasedAmount ?? 0m,
                refundedAmount,
                penaltyAmount,
                serviceFees,
                remainingAmount,
                escrow?.Status),
            new AdminConversationReferencesResponse(
                conversations.FirstOrDefault(item =>
                    item.ConversationType == (int)ConversationType.ContractWorkroom &&
                    !item.DisputesId.HasValue)?.ConversationsId,
                conversations.FirstOrDefault(item => item.DisputesId == dispute.DisputesId)?.ConversationsId),
            auditTrail,
            decisions,
            penalties,
            auditTrail.FirstOrDefault(item => item.Action == "Dispute.FinalResolution")?.AuditId);
    }

    public static async Task NotifyParticipantsAsync(
        INotificationService notifications,
        ILogger logger,
        Contract contract,
        Dispute dispute,
        string content,
        CancellationToken cancellationToken)
    {
        var participantIds = new[]
            {
                contract.ClientProfiles.UserId,
                contract.FreelancerProfiles?.UserId
            }
            .Where(userId => userId.HasValue && userId.Value != Guid.Empty)
            .Select(userId => userId!.Value)
            .Distinct();

        foreach (var participantId in participantIds)
        {
            try
            {
                await notifications.CreateNotificationAsync(
                    participantId,
                    NotificationType.DisputeUpdate,
                    "Dispute case updated",
                    content,
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Dispute {DisputeId} was updated, but notification delivery to user {UserId} failed.",
                    dispute.DisputesId,
                    participantId);
            }
        }
    }
}
