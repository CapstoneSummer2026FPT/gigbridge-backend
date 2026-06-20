using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.Internal;

internal static class ESignDocumentProjection
{
    public static async Task<ESignDocumentResponse> ToResponseAsync(
        IApplicationDbContext context,
        EsignDocument document,
        CancellationToken cancellationToken)
    {
        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature => signature.EsignDocumentsId == document.EsignDocumentsId)
            .OrderBy(signature => signature.CreatedAt)
            .ToListAsync(cancellationToken);

        return ToResponse(document, signatures);
    }

    public static ESignDocumentResponse ToResponse(
        EsignDocument document,
        IReadOnlyList<EsignSignature> signatures)
    {
        return new ESignDocumentResponse(
            document.EsignDocumentsId,
            document.JobPostsId,
            document.ContractsId,
            document.EsignTemplatesId,
            document.DocumentCode,
            document.RenderedHtmlContent,
            document.Status,
            document.DocumentHash,
            document.ExpiresAt,
            document.FinalizedAt,
            document.ExportedPdfUrl,
            document.CreatedAt,
            document.UpdatedAt,
            signatures.Select(ToSignatureResponse).ToList());
    }

    public static ESignSignatureResponse ToSignatureResponse(EsignSignature signature)
    {
        return new ESignSignatureResponse(
            signature.EsignSignaturesId,
            signature.EsignDocumentsId,
            signature.UserId,
            signature.SignerRole,
            signature.SignatureImageUrl,
            signature.SignatureWidth,
            signature.SignatureHeight,
            signature.Status,
            signature.SignedAt,
            signature.DeclinedAt,
            signature.DeclineReason,
            signature.IpAddress,
            signature.UserAgent,
            signature.CreatedAt);
    }
}
