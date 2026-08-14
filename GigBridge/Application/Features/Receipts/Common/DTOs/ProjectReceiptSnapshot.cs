namespace Application.Features.Receipts.Common.DTOs;

public sealed record ProjectReceiptSnapshot(
    Guid ReceiptId,
    string ReceiptNumber,
    int ReceiptType,
    DateTime IssuedAtUtc,
    string ReceiptStatus,
    ProjectReceiptPartySnapshot Client,
    ProjectReceiptPartySnapshot Freelancer,
    string ContractCode,
    Guid ContractId,
    string ProjectTitle,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    DateTime CompletedAtUtc,
    decimal ContractValueGigCoin,
    decimal ClientServiceFeeGigCoin,
    decimal ClientTotalPaidGigCoin,
    decimal TotalReleasedBeforeCompletionGigCoin,
    decimal FinalReleaseGigCoin,
    decimal TotalReleasedGigCoin,
    decimal EscrowRemainingGigCoin,
    decimal FreelancerServiceFeeGigCoin,
    decimal TotalGrossReleasedGigCoin,
    decimal NetReceivedBeforeCompletionGigCoin,
    decimal FinalNetReceivedGigCoin,
    decimal FreelancerNetReceivedGigCoin,
    string FinalTransactionReference,
    decimal VndPerGigCoin,
    IReadOnlyList<ProjectReceiptMilestoneSnapshot> Milestones,
    DateTime? ProjectStartedAtUtc = null);

public sealed record ProjectReceiptPartySnapshot(
    Guid UserId,
    string FullName,
    string CompanyName,
    string Email,
    string? IdentityOrTaxCode = null);

public sealed record ProjectReceiptMilestoneSnapshot(
    int Number,
    Guid MilestoneId,
    string Title,
    DateTime? ApprovedAtUtc,
    decimal ValueGigCoin,
    decimal ReleasedBeforeCompletionGigCoin,
    decimal FinalReleaseGigCoin,
    decimal TotalReleasedGigCoin,
    decimal ServiceFeeGigCoin,
    decimal NetReceivedGigCoin,
    DateTime? StartedAtUtc = null,
    DateOnly? DueDate = null);
