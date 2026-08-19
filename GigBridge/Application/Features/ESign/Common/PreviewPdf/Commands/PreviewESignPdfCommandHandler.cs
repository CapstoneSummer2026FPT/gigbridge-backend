using Application.Common.InternalServices.ESign.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Common.InternalServices.ESign.Services;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
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

        var content = await ESignAccessGuard.GetContentAsync(context, document.EsignDocumentsId, cancellationToken);
        var snapshot = ContractEsignRenderer.GetSnapshot(content);
        var signerRole = snapshot.Client.UserId == request.UserId
            ? ESignerRole.Client
            : snapshot.Freelancer.UserId == request.UserId
                ? ESignerRole.Freelancer
                : throw new ForbiddenAccessException("Only contract participants can preview a signature.");
        var counterpartParty = signerRole == ESignerRole.Client
            ? snapshot.Freelancer
            : snapshot.Client;

        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId)
            .ToListAsync(cancellationToken);

        if (document.Status == (int)ESignDocumentStatus.FullySigned ||
            signatures.Any(signature =>
                signature.UserId == request.UserId &&
                signature.Status == (int)ESignSignatureStatus.Signed))
        {
            throw new ConflictException("This user has already signed the contract.");
        }

        var participantIdentityCodes = await context.Set<User>()
            .AsNoTracking()
            .Where(user =>
                user.UserId == request.UserId ||
                user.UserId == counterpartParty.UserId)
            .Select(user => new { user.UserId, user.IdentityOrTaxCode })
            .ToListAsync(cancellationToken);
        var storedIdentityCode = participantIdentityCodes
            .SingleOrDefault(user => user.UserId == request.UserId)
            ?.IdentityOrTaxCode;
        var identityCode = ContractIdentityCode.Normalize(
            ContractIdentityCode.IsValid(storedIdentityCode)
                ? storedIdentityCode
                : request.Request.IdentityOrTaxCode);
        var counterpartSignatureIdentityCode = signatures
            .SingleOrDefault(signature =>
                signature.UserId == counterpartParty.UserId &&
                signature.Status is (int)ESignSignatureStatus.Pending or
                    (int)ESignSignatureStatus.Signed)
            ?.IdentityOrTaxCode;
        var counterpartStoredIdentityCode = participantIdentityCodes
            .SingleOrDefault(user => user.UserId == counterpartParty.UserId)
            ?.IdentityOrTaxCode;

        ContractIdentityCode.EnsureDifferentParties(
            identityCode,
            counterpartSignatureIdentityCode,
            counterpartStoredIdentityCode,
            counterpartParty.IdentityOrTaxCode);

        var existingDraft = signatures.SingleOrDefault(signature =>
            signature.UserId == request.UserId &&
            signature.Status == (int)ESignSignatureStatus.Pending);
        var signatureImageUrl = request.Request.SignatureImageUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(signatureImageUrl))
        {
            // Validate the temporary image without uploading or persisting it.
            SignatureImageStorage.ParseImageDataUri(signatureImageUrl);
        }
        else
        {
            signatureImageUrl = existingDraft?.SignatureImageUrl;
        }

        if (string.IsNullOrWhiteSpace(signatureImageUrl))
        {
            throw new BadRequestException("Signature is required for the first draft preview.");
        }

        var previewSignature = new ContractSignatureSnapshot(
            request.UserId,
            (int)signerRole,
            signatureImageUrl,
            request.Request.SignatureWidth ?? existingDraft?.SignatureWidth,
            request.Request.SignatureHeight ?? existingDraft?.SignatureHeight,
            DateTime.UtcNow,
            request.IpAddress,
            request.UserAgent,
            snapshot.PolicyVersion,
            DateTime.UtcNow,
            false);

        var counterpart = signatures.SingleOrDefault(signature =>
            signature.UserId != request.UserId &&
            (signature.Status == (int)ESignSignatureStatus.Signed ||
             IsValidPendingDraft(signature, snapshot.PolicyVersion)));
        var counterpartSnapshot = counterpart is null
            ? null
            : counterpart.Status == (int)ESignSignatureStatus.Signed
                ? ContractEsignRenderer.ToSignatureSnapshot(counterpart)
                : ContractEsignRenderer.ToDraftSignatureSnapshot(counterpart);

        var clientSignature = signerRole == ESignerRole.Client
            ? previewSignature
            : counterpartSnapshot?.SignerRole == (int)ESignerRole.Client
                ? counterpartSnapshot
                : null;
        var freelancerSignature = signerRole == ESignerRole.Freelancer
            ? previewSignature
            : counterpartSnapshot?.SignerRole == (int)ESignerRole.Freelancer
                ? counterpartSnapshot
                : null;

        var previewSnapshot = ContractEsignRenderer.WithIdentityCodes(
            snapshot,
            signerRole == ESignerRole.Client
                ? identityCode
                : counterpart?.SignerRole == (int)ESignerRole.Client
                    ? counterpart.IdentityOrTaxCode
                    : snapshot.Client.IdentityOrTaxCode,
            signerRole == ESignerRole.Freelancer
                ? identityCode
                : counterpart?.SignerRole == (int)ESignerRole.Freelancer
                    ? counterpart.IdentityOrTaxCode
                    : snapshot.Freelancer.IdentityOrTaxCode);

        var wordDocument = await documentGenerator.GenerateAsync(
            previewSnapshot,
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
            "Gigbridge-Client-Freelancer-Contract-BAN-XEM-TRUOC.pdf",
            "application/pdf");
    }

    private static bool IsValidPendingDraft(EsignSignature signature, string policyVersion) =>
        signature.Status == (int)ESignSignatureStatus.Pending &&
        signature.DraftSubmittedAt.HasValue &&
        !string.IsNullOrWhiteSpace(signature.SignatureImageUrl) &&
        ContractIdentityCode.IsValid(signature.IdentityOrTaxCode) &&
        signature.PolicyAcceptedAt.HasValue &&
        string.Equals(signature.PolicyVersion, policyVersion, StringComparison.Ordinal);
}
