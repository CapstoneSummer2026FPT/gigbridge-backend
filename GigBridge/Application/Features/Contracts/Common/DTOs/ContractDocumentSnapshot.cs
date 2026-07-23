namespace Application.Features.Contracts.Common.DTOs;

public sealed record ContractDocumentSnapshot(
    Guid DocumentId,
    Guid ContractId,
    Guid JobPostId,
    Guid? ProposalId,
    Guid? FinalOfferId,
    string ContractCode,
    int ContractVersion,
    int TemplateVersion,
    string PolicyVersion,
    DateTime CreatedAtUtc,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string ProjectTitle,
    string? ProjectDescription,
    string? ScopeOfWork,
    string? OutOfScope,
    decimal TotalContractValueVnd,
    ContractPartySnapshot Client,
    ContractPartySnapshot Freelancer,
    IReadOnlyList<ContractMilestoneSnapshot> Milestones,
    string? PreviousDocumentHash = null);

public sealed record ContractPartySnapshot(
    Guid UserId,
    Guid ProfileId,
    string FullName,
    string Email,
    string? Phone,
    string? Address,
    string? IdentityOrTaxCode = null,
    string? Representative = null,
    string? RepresentativeTitle = null,
    string? MaskedBankAccount = null);

public sealed record ContractMilestoneSnapshot(
    int Number,
    string Title,
    string? Description,
    string? Deliverable,
    string? AcceptanceCriteria,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? RevisionLimit,
    decimal ValueVnd);

public sealed record ContractSignatureSnapshot(
    Guid UserId,
    int SignerRole,
    string SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight,
    DateTime SignedAtUtc,
    string? IpAddress,
    string? UserAgent,
    string? PolicyVersion,
    DateTime? PolicyAcceptedAtUtc);

public sealed record GeneratedContractDocument(byte[] Content, string FileName, string MimeType);

public sealed record ContractEsignDeliveryPayload(
    Guid DocumentId,
    string Email,
    string RecipientName,
    string ContractTitle);
