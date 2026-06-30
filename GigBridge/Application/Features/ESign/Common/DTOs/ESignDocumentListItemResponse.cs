namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignDocumentListItemResponse(
    Guid DocumentId,
    Guid JobPostId,
    Guid? ContractId,
    string DocumentCode,
    string DocumentType,
    string Title,
    int DocumentStatus,
    int CurrentUserSignerRole,
    DateTime? CurrentUserSignedAt,
    bool HasClientSigned,
    bool HasFreelancerSigned,
    int SignatureCount,
    DateTime? FinalizedAt,
    string? ExportedPdfUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
