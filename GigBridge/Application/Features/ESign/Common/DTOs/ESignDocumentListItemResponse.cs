namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignDocumentListItemResponse(
    Guid DocumentId,
    Guid JobPostId,
    Guid? ContractId,
    string DocumentCode,
    string DocumentType,
    string Title,
    int DocumentStatus,
    int? CurrentUserSignerRole,
    DateTime? CurrentUserSignedAt,
    bool CanCurrentUserSign,
    bool HasClientSigned,
    bool HasFreelancerSigned,
    int SignatureCount,
    DateTime? FinalizedAt,
    string? ExportedPdfUrl,
    bool HasFinalArtifact,
    string? FinalizedDocumentFileName,
    bool HasPdfArtifact,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
