namespace Application.Features.ESign.Signatures.Submit.DTOs;

public sealed record SubmitESignSignatureRequest(
    Guid DocumentId,
    string SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight);
