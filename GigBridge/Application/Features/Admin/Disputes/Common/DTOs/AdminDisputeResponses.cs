using Application.Features.Disputes.Common.DTOs;

namespace Application.Features.Admin.Disputes.Common.DTOs;

public sealed record AdminDisputePartyResponse(
    Guid UserId,
    Guid ProfileId,
    string FullName,
    string Email);

public sealed record AdminDisputeListItemResponse(
    Guid DisputeId,
    Guid ContractId,
    string ContractTitle,
    string InitiatorName,
    string? InitiatorRole,
    string ClientName,
    string? FreelancerName,
    Guid? MilestoneId,
    string? MilestoneTitle,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionLabel,
    int EvidenceCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt);

public sealed record AdminDisputeListResponse(
    IReadOnlyList<AdminDisputeListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminRelatedReportResponse(
    Guid ReportId,
    int IssueType,
    string Description,
    string DesiredResolution,
    int Status,
    DateTime CreatedAt);

public sealed record AdminContractSummaryResponse(
    decimal TotalBudget,
    DateTime CreatedAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime? CompletedAt,
    int ProgressPercentage);

public sealed record AdminJobQuestionResponse(string Question, string? AcceptedAnswer);

public sealed record AdminProposalMilestoneResponse(
    string Title,
    string? Description,
    decimal Amount,
    string? EstimatedDuration,
    string? Deliverables,
    string? AcceptanceCriteria,
    int OrderIndex);

public sealed record AdminOriginalJobResponse(
    Guid JobPostId,
    string Title,
    string Description,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? Duration,
    string? Category,
    IReadOnlyList<string> Skills,
    decimal? ProposalAmount,
    string? ProposalDuration,
    IReadOnlyList<AdminJobQuestionResponse> Questions,
    IReadOnlyList<AdminProposalMilestoneResponse> ProposedMilestones);

public sealed record AdminMilestoneAttachmentResponse(
    Guid AttachmentId,
    string FileName,
    string FileUrl,
    long? FileSize,
    string? MimeType,
    Guid? UploadedByUserId,
    DateTime CreatedAt);

public sealed record AdminMilestoneResponse(
    Guid MilestoneId,
    string Title,
    string? Description,
    decimal Amount,
    decimal ReleasedAmount,
    decimal AllocatableAmount,
    int Status,
    string? Deliverables,
    string? SubmissionDescription,
    DateOnly? DueDate,
    DateTime? StartedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? PaidAt,
    IReadOnlyList<AdminMilestoneAttachmentResponse> Attachments);

public sealed record AdminEscrowSummaryResponse(
    Guid? EscrowId,
    decimal OriginalEscrow,
    decimal FundedAmount,
    decimal ReleasedAmount,
    decimal RefundedAmount,
    decimal ServiceFeeAmount,
    decimal RemainingAmount,
    int? Status);

public sealed record AdminConversationReferencesResponse(
    Guid? WorkspaceConversationId,
    Guid? DisputeConversationId);

public sealed record AdminAuditEventResponse(
    Guid AuditId,
    Guid AdminId,
    string Action,
    string? OldValues,
    string? NewValues,
    DateTime CreatedAt);

public sealed record AdminMilestoneDecisionResponse(
    Guid DecisionId,
    Guid MilestoneId,
    int Outcome,
    decimal MilestoneAmount,
    decimal ReleasedBeforeDecision,
    decimal AdditionalRelease,
    decimal Refund,
    Guid DecidedByAdminId,
    DateTime CreatedAt);

public sealed record AdminDisputeDetailResponse(
    Guid DisputeId,
    Guid ContractId,
    string ContractTitle,
    int ContractStatus,
    Guid InitiatorId,
    string InitiatorName,
    string? InitiatorRole,
    AdminDisputePartyResponse Client,
    AdminDisputePartyResponse? Freelancer,
    Guid? MilestoneId,
    string? MilestoneTitle,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionLabel,
    string? ResolutionNote,
    Guid? ResolvedByAdminId,
    Guid? AssignedAdminId,
    DateTime? AssignedAt,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DisputeEvidenceResponse> Evidence,
    string? Title,
    string? Description,
    decimal? ClaimedAmount,
    string? RequestedResolution,
    int Urgency,
    Guid? RespondentId,
    string? RespondentName,
    string? AssignedAdminName,
    AdminRelatedReportResponse? RelatedReport,
    AdminContractSummaryResponse Contract,
    AdminOriginalJobResponse OriginalJob,
    IReadOnlyList<AdminMilestoneResponse> Milestones,
    AdminEscrowSummaryResponse Escrow,
    AdminConversationReferencesResponse Conversations,
    IReadOnlyList<AdminAuditEventResponse> AuditTrail,
    IReadOnlyList<AdminMilestoneDecisionResponse> MilestoneDecisions);
