using Application.Common.InternalServices.ESign.Services;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.GeneratePdf.Commands;

public sealed class GenerateESignPdfCommandHandler(
    IApplicationDbContext context,
    IContractEsignDocumentGenerator documentGenerator,
    IWordToPdfConverter pdfConverter)
    : IRequestHandler<GenerateESignPdfCommand, ESignPdfArtifactResponse>
{
    public async Task<ESignPdfArtifactResponse> Handle(
        GenerateESignPdfCommand request,
        CancellationToken cancellationToken)
    {
        var document = await ESignAccessGuard.GetDocumentAsync(context, request.DocumentId, cancellationToken);
        await ESignAccessGuard.EnsureCanViewDocumentAsync(context, document, request.UserId, cancellationToken);

        if (!document.ContractsId.HasValue)
        {
            throw new BadRequestException("This document does not use the contract Word template.");
        }

        var content = await ESignDocumentContentStorage.GetAsync(
            context,
            document.EsignDocumentsId,
            cancellationToken);

        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed)
            .OrderBy(signature => signature.SignerRole)
            .ToListAsync(cancellationToken);
        var signatureCount = signatures.Count;
        var expectedHash = ESignPdfArtifactRevision.ExpectedHash(document);
        if (document.PdfDocumentSizeBytes is > 0 &&
            document.PdfSignatureCount == signatureCount &&
            string.Equals(document.PdfDocumentHash, expectedHash, StringComparison.Ordinal))
        {
            return ToResponse(document);
        }

        var snapshot = ContractEsignRenderer.GetSnapshot(content);
        var clientSignature = signatures
            .Where(signature => signature.SignerRole == (int)ESignerRole.Client)
            .Select(ContractEsignRenderer.ToSignatureSnapshot)
            .SingleOrDefault();
        var freelancerSignature = signatures
            .Where(signature => signature.SignerRole == (int)ESignerRole.Freelancer)
            .Select(ContractEsignRenderer.ToSignatureSnapshot)
            .SingleOrDefault();
        var wordDocument = await documentGenerator.GenerateAsync(
            snapshot,
            clientSignature,
            freelancerSignature,
            document.DocumentHash ?? string.Empty,
            cancellationToken);
        var pdf = await pdfConverter.ConvertAsync(
            wordDocument.Content,
            wordDocument.FileName,
            cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ESignDocumentLock.ForDocument(document.EsignDocumentsId),
            cancellationToken);

        var currentDocument = await context.Set<EsignDocument>()
            .AsNoTracking()
            .Where(item => item.EsignDocumentsId == document.EsignDocumentsId)
            .Select(item => new { item.DocumentHash, item.ContentRevision })
            .SingleAsync(cancellationToken);
        var currentSignatureCount = await context.Set<EsignSignature>()
            .AsNoTracking()
            .CountAsync(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed,
                cancellationToken);
        if (currentSignatureCount != signatureCount ||
            currentDocument.ContentRevision != document.ContentRevision ||
            !string.Equals(currentDocument.DocumentHash, document.DocumentHash, StringComparison.Ordinal))
        {
            throw new ConflictException("The document signatures changed while the PDF was being prepared. Please retry.");
        }

        var generatedAt = DateTime.UtcNow;
        document.PdfSignatureCount = signatureCount;
        document.PdfDocumentHash = expectedHash;
        document.PdfDocumentSizeBytes = pdf.LongLength;
        ESignDocumentRevision.Advance(document, generatedAt);
        await ESignDocumentRevision.EnqueueAsync(
            context,
            document,
            generatedAt,
            cancellationToken);
        await ESignArtifactStorage.UpsertAsync(
            context,
            document,
            ESignArtifactType.Pdf,
            pdf,
            ESignPdfArtifactRevision.ContractFileName,
            "application/pdf",
            generatedAt,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(document);
    }

    private static ESignPdfArtifactResponse ToResponse(EsignDocument document) =>
        new(
            document.EsignDocumentsId,
            ESignPdfArtifactRevision.ContractFileName);
}
