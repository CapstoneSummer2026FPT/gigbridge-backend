namespace Application.Features.ESign.Common.PreviewPdf.DTOs;

public sealed record PreviewESignPdfRequest(
    string? SignatureImageUrl,
    int? SignatureWidth,
    int? SignatureHeight,
    string IdentityOrTaxCode);
