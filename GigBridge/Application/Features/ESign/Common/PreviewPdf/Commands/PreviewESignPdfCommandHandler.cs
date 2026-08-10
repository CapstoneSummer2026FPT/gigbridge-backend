using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.PreviewPdf.Commands;

public sealed class PreviewESignPdfCommandHandler(
    IApplicationDbContext context,
    IContractEsignDocumentGenerator documentGenerator,
    IWordToPdfConverter pdfConverter)
    : IRequestHandler<PreviewESignPdfCommand, ESignDocumentDownloadResponse>
{
    public async Task<ESignDocumentDownloadResponse> Handle(
        PreviewESignPdfCommand request,
        CancellationToken cancellationToken)
    {
        var document = await ESignAccessGuard.GetDocumentAsync(
            context,
            request.DocumentId,
            cancellationToken);
        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            context,
            document,
            request.UserId,
            cancellationToken);

        if (!document.ContractsId.HasValue)
        {
            throw new BadRequestException("This document does not use the contract Word template.");
        }

        // Validate the temporary image without uploading or persisting it.
        SignatureImageStorage.ParseImageDataUri(request.Request.SignatureImageUrl);

        var snapshot = ContractEsignRenderer.GetSnapshot(document);
        var signerRole = snapshot.Client.UserId == request.UserId
            ? ESignerRole.Client
            : snapshot.Freelancer.UserId == request.UserId
                ? ESignerRole.Freelancer
                : throw new ForbiddenAccessException("Only contract participants can preview a signature.");

        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed)
            .ToListAsync(cancellationToken);

        if (signatures.Any(signature => signature.UserId == request.UserId))
        {
            throw new ConflictException("This user has already signed the contract.");
        }

        var previewSignature = new ContractSignatureSnapshot(
            request.UserId,
            (int)signerRole,
            request.Request.SignatureImageUrl,
            request.Request.SignatureWidth,
            request.Request.SignatureHeight,
            DateTime.UtcNow,
            request.IpAddress,
            request.UserAgent,
            snapshot.PolicyVersion,
            DateTime.UtcNow);

        var clientSignature = signerRole == ESignerRole.Client
            ? previewSignature
            : signatures
                .Where(signature => signature.SignerRole == (int)ESignerRole.Client)
                .Select(ContractEsignRenderer.ToSignatureSnapshot)
                .SingleOrDefault();
        var freelancerSignature = signerRole == ESignerRole.Freelancer
            ? previewSignature
            : signatures
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

        return new ESignDocumentDownloadResponse(
            pdf,
            "Gigbridge-Client-Freelancer-Contract-preview.pdf",
            "application/pdf");
    }
}
