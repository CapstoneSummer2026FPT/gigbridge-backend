using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.SavePdf.Commands;

public sealed class SaveESignPdfCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SaveESignPdfCommand, ESignPdfArtifactResponse>
{
    private const int MaxPdfBytes = 20 * 1024 * 1024;
    public async Task<ESignPdfArtifactResponse> Handle(
        SaveESignPdfCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Content.Length is < 5 or > MaxPdfBytes ||
            request.Content[0] != (byte)'%' ||
            request.Content[1] != (byte)'P' ||
            request.Content[2] != (byte)'D' ||
            request.Content[3] != (byte)'F' ||
            request.Content[4] != (byte)'-')
        {
            throw new BadRequestException("A valid PDF file of 20 MB or less is required.");
        }

        var document = await ESignAccessGuard.GetDocumentAsync(
            context,
            request.DocumentId,
            cancellationToken);
        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            context,
            document,
            request.UserId,
            cancellationToken);
        if (document.ContractsId.HasValue)
        {
            throw new BadRequestException("Contract PDFs must be generated from the Word template.");
        }

        var content = await ESignAccessGuard.GetContentAsync(context, document.EsignDocumentsId, cancellationToken);

        var signedCount = await context.Set<EsignSignature>()
            .CountAsync(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed,
                cancellationToken);
        if (request.SignatureCount != signedCount)
        {
            throw new ConflictException("The document signatures changed while the PDF was being prepared. Please retry.");
        }

        var now = DateTime.UtcNow;
        var safeBaseName = Path.GetFileNameWithoutExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(safeBaseName)) safeBaseName = document.DocumentCode;
        content.PdfDocumentContent = request.Content;
        content.PdfDocumentFileName = $"{safeBaseName}.pdf";
        document.PdfSignatureCount = signedCount;
        document.PdfDocumentHash = ESignPdfArtifactRevision.ExpectedHash(document);
        document.PdfDocumentSizeBytes = request.Content.LongLength;
        document.ContentRevision++;
        document.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return new ESignPdfArtifactResponse(
            document.EsignDocumentsId,
            content.PdfDocumentFileName);
    }
}
