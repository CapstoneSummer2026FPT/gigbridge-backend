namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignSignatureResponse(
    Guid SignatureId,
    Guid DocumentId,
    Guid UserId,
    int SignerRole,
    string? SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight,
    string? IdentityOrTaxCode,
    bool IsDraftValid,
    int Status,
    DateTime? SignedAt,
    DateTime? DraftSubmittedAt,
    DateTime? DeclinedAt,
    string? DeclineReason,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
