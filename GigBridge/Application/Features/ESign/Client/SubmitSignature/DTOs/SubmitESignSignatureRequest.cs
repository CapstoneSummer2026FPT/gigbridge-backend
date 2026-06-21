namespace Application.Features.ESign.Client.SubmitSignature.DTOs;

public sealed record SubmitESignSignatureRequest(
    Guid DocumentId,
    string SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight);
