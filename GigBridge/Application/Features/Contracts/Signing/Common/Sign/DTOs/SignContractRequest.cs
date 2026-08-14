namespace Application.Features.Contracts.Signing.Common.Sign.DTOs;

public sealed record SignContractRequest(
    string? SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight,
    string IdentityOrTaxCode,
    bool PolicyAccepted,
    string? PolicyVersion,
    string? IdentityVerificationTicket = null);
