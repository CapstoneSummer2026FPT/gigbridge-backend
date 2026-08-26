using Application.Common.InternalServices.ESign.Services;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace Application.Features.ESign.Common.DownloadDocument.Queries;

public sealed class DownloadESignDocumentQueryHandler
    : IRequestHandler<DownloadESignDocumentQuery, ESignDocumentDownloadResponse>
{
    private readonly IApplicationDbContext _context;

    public DownloadESignDocumentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ESignDocumentDownloadResponse> Handle(
        DownloadESignDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await ESignAccessGuard.GetDocumentAsync(
            _context,
            request.DocumentId,
            cancellationToken);

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);

        var signedCount = await _context.Set<EsignSignature>()
            .CountAsync(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed,
                cancellationToken);
        if (document.PdfDocumentSizeBytes is not > 0 ||
            document.PdfSignatureCount != signedCount ||
            !string.Equals(
                document.PdfDocumentHash,
                ESignPdfArtifactRevision.ExpectedHash(document),
                StringComparison.Ordinal))
        {
            throw new ConflictException("The current PDF has not been prepared yet.");
        }

        var artifact = await ESignArtifactStorage.GetAsync(
            _context,
            document.EsignDocumentsId,
            ESignArtifactType.Pdf,
            "ESign.Artifact.Pdf.Download",
            cancellationToken);
        if (artifact?.Content is not { Length: > 0 })
        {
            throw new ConflictException("The current PDF has not been prepared yet.");
        }

        var fileName = document.ContractsId.HasValue
            ? ESignPdfArtifactRevision.ContractFileName
            : Path.GetFileName(artifact.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{document.DocumentCode}.pdf";
        }

        return new ESignDocumentDownloadResponse(
            artifact.Content,
            fileName,
            "application/pdf");
    }
}
