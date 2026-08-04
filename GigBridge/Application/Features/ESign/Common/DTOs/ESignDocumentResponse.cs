namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignDocumentResponse(
    Guid DocumentId,
    Guid JobPostId,
    Guid? ContractId,
    Guid TemplateId,
    string DocumentCode,
    string RenderedHtmlContent,
    int Status,
    string? DocumentHash,
    DateTime? ExpiresAt,
    DateTime? FinalizedAt,
    string? ExportedPdfUrl,
    int? CurrentUserSignerRole,
    bool CanCurrentUserSign,
    bool HasFinalArtifact,
    string? FinalizedDocumentFileName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ESignSignatureResponse> Signatures);
