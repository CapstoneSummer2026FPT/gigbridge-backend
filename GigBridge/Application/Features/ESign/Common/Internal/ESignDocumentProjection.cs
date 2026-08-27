using Application.Common.Exceptions;
using Application.Common.InternalServices.ESign.Services;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ESign.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.Internal;

internal static class ESignDocumentProjection
{
    public static async Task<ESignDocumentResponse> ToResponseAsync(
        IApplicationDbContext context,
        EsignDocument document,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature => signature.EsignDocumentsId == document.EsignDocumentsId)
            .OrderBy(signature => signature.CreatedAt)
            .ToListAsync(cancellationToken);

        var signerRole = await ResolveSignerRoleAsync(
            context,
            document,
            currentUserId,
            cancellationToken);

        var renderedHtmlContent = await context.Set<EsignDocumentContent>()
            .AsNoTracking()
            .Where(content => content.EsignDocumentsId == document.EsignDocumentsId)
            .Select(content => content.RenderedHtmlContent)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("E-sign document content does not exist.");

        return ToResponse(document, renderedHtmlContent, signatures, currentUserId, signerRole);
    }

    public static async Task<ESignDocumentStatusResponse> ToStatusResponseAsync(
        IApplicationDbContext context,
        EsignDocument document,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature => signature.EsignDocumentsId == document.EsignDocumentsId)
            .OrderBy(signature => signature.CreatedAt)
            .ToListAsync(cancellationToken);

        var signerRole = await ResolveSignerRoleAsync(
            context,
            document,
            currentUserId,
            cancellationToken);

        return ToStatusResponse(document, signatures, currentUserId, signerRole);
    }

    public static async Task<ESignDocumentLightweightStatusResponse> ToLightweightStatusResponseAsync(
        IApplicationDbContext context,
        EsignDocument document,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var signatures = await context.Set<EsignSignature>()
            .AsNoTracking()
            .TagWith("ESign.Status.Signatures")
            .Where(signature => signature.EsignDocumentsId == document.EsignDocumentsId)
            .OrderBy(signature => signature.CreatedAt)
            .Select(signature => new ESignSignerStatusResponse(
                signature.EsignSignaturesId,
                signature.EsignDocumentsId,
                signature.UserId,
                signature.SignerRole,
                signature.Status,
                signature.Status == (int)ESignSignatureStatus.Pending &&
                    signature.DraftSubmittedAt.HasValue &&
                    signature.SignatureImageUrl != null &&
                    signature.SignatureImageUrl != string.Empty &&
                    signature.IdentityOrTaxCode != null &&
                    (signature.IdentityOrTaxCode.Length == 9 || signature.IdentityOrTaxCode.Length == 12) &&
                    signature.PolicyAcceptedAt.HasValue &&
                    signature.PolicyVersion == ContractEsignRenderer.PolicyVersion,
                signature.SignedAt,
                signature.DraftSubmittedAt,
                signature.UserId == currentUserId ? signature.SignatureImageUrl : null,
                signature.UserId == currentUserId ? signature.SignatureWidth : null,
                signature.UserId == currentUserId ? signature.SignatureHeight : null,
                signature.UserId == currentUserId ? signature.IdentityOrTaxCode : null))
            .ToListAsync(cancellationToken);

        var signerRole = await ResolveSignerRoleAsync(
            context,
            document,
            currentUserId,
            cancellationToken);
        var signedCount = signatures.Count(signature =>
            signature.Status == (int)ESignSignatureStatus.Signed);
        var hasCurrentPdf = document.PdfDocumentSizeBytes is > 0 &&
            document.PdfSignatureCount == signedCount &&
            string.Equals(
                document.PdfDocumentHash,
                ESignPdfArtifactRevision.ExpectedHash(document),
                StringComparison.Ordinal);
        var hasCurrentUserSigned = signatures.Any(signature =>
            signature.UserId == currentUserId &&
            signature.Status == (int)ESignSignatureStatus.Signed);

        return new ESignDocumentLightweightStatusResponse(
            document.EsignDocumentsId,
            document.ContractsId,
            document.Status,
            document.ContentRevision,
            document.CreatedAt,
            document.UpdatedAt,
            document.ExpiresAt,
            document.FinalizedAt,
            signerRole,
            signerRole.HasValue &&
                document.Status is (int)ESignDocumentStatus.PendingSignatures or
                    (int)ESignDocumentStatus.PartiallySigned &&
                !hasCurrentUserSigned,
            document.FinalizedDocumentSizeBytes is > 0,
            hasCurrentPdf,
            hasCurrentPdf ? document.PdfDocumentSizeBytes : null,
            document.PdfDocumentHash,
            signatures.Count,
            signatures);
    }

    public static ESignDocumentStatusResponse ToStatusResponse(
        EsignDocument document,
        IReadOnlyList<EsignSignature> signatures,
        Guid currentUserId,
        int? signerRole)
    {
        var hasCurrentUserSigned = signatures.Any(signature =>
            signature.UserId == currentUserId &&
            signature.Status == (int)ESignSignatureStatus.Signed);
        var canCurrentUserSign = signerRole.HasValue &&
            document.Status is (int)ESignDocumentStatus.PendingSignatures or (int)ESignDocumentStatus.PartiallySigned &&
            !hasCurrentUserSigned;
        var signedCount = signatures.Count(signature =>
            signature.Status == (int)ESignSignatureStatus.Signed);
        var hasCurrentPdf = document.PdfDocumentSizeBytes is > 0 &&
            document.PdfSignatureCount == signedCount &&
            string.Equals(
                document.PdfDocumentHash,
                ESignPdfArtifactRevision.ExpectedHash(document),
                StringComparison.Ordinal);

        return new ESignDocumentStatusResponse(
            document.EsignDocumentsId,
            document.JobPostsId,
            document.ContractsId,
            document.EsignTemplatesId,
            document.DocumentCode,
            document.Status,
            document.DocumentHash,
            document.ExpiresAt,
            document.FinalizedAt,
            document.ExportedPdfUrl,
            signerRole,
            canCurrentUserSign,
            document.FinalizedDocumentSizeBytes is > 0,
            document.FinalizedDocumentFileName,
            hasCurrentPdf,
            document.CreatedAt,
            document.UpdatedAt,
            signatures.Select(signature => ToSignatureResponse(signature, currentUserId)).ToList(),
            document.ContentRevision);
    }

    public static ESignDocumentResponse ToResponse(
        EsignDocument document,
        string renderedHtmlContent,
        IReadOnlyList<EsignSignature> signatures,
        Guid currentUserId,
        int? signerRole)
    {
        var hasCurrentUserSigned = signatures.Any(signature =>
            signature.UserId == currentUserId &&
            signature.Status == (int)ESignSignatureStatus.Signed);
        var canCurrentUserSign = signerRole.HasValue &&
            document.Status is (int)ESignDocumentStatus.PendingSignatures or (int)ESignDocumentStatus.PartiallySigned &&
            !hasCurrentUserSigned;
        var signedCount = signatures.Count(signature =>
            signature.Status == (int)ESignSignatureStatus.Signed);
        var hasCurrentPdf = document.PdfDocumentSizeBytes is > 0 &&
            document.PdfSignatureCount == signedCount &&
            string.Equals(
                document.PdfDocumentHash,
                ESignPdfArtifactRevision.ExpectedHash(document),
                StringComparison.Ordinal);

        return new ESignDocumentResponse(
            document.EsignDocumentsId,
            document.JobPostsId,
            document.ContractsId,
            document.EsignTemplatesId,
            document.DocumentCode,
            renderedHtmlContent,
            document.Status,
            document.DocumentHash,
            document.ExpiresAt,
            document.FinalizedAt,
            document.ExportedPdfUrl,
            signerRole,
            canCurrentUserSign,
            document.FinalizedDocumentSizeBytes is > 0,
            document.FinalizedDocumentFileName,
            hasCurrentPdf,
            document.CreatedAt,
            document.UpdatedAt,
            signatures.Select(signature => ToSignatureResponse(signature, currentUserId)).ToList(),
            document.ContentRevision);
    }

    public static ESignSignatureResponse ToSignatureResponse(
        EsignSignature signature,
        Guid currentUserId)
    {
        return new ESignSignatureResponse(
            signature.EsignSignaturesId,
            signature.EsignDocumentsId,
            signature.UserId,
            signature.SignerRole,
            signature.SignatureImageUrl,
            signature.SignatureWidth,
            signature.SignatureHeight,
            currentUserId == signature.UserId
                ? signature.IdentityOrTaxCode
                : null,
            signature.Status == (int)ESignSignatureStatus.Pending &&
                signature.DraftSubmittedAt.HasValue &&
                !string.IsNullOrWhiteSpace(signature.SignatureImageUrl) &&
                ContractIdentityCode.IsValid(signature.IdentityOrTaxCode) &&
                signature.PolicyAcceptedAt.HasValue &&
                string.Equals(
                    signature.PolicyVersion,
                    ContractEsignRenderer.PolicyVersion,
                    StringComparison.Ordinal),
            signature.Status,
            signature.SignedAt,
            signature.DraftSubmittedAt,
            signature.DeclinedAt,
            signature.DeclineReason,
            signature.IpAddress,
            signature.UserAgent,
            signature.CreatedAt,
            signature.UpdatedAt);
    }

    private static async Task<int?> ResolveSignerRoleAsync(
        IApplicationDbContext context,
        EsignDocument document,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var userRole = await context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == currentUserId)
            .Select(user => (int?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (userRole == (int)UserRole.Admin)
        {
            return null;
        }

        if (userRole is (int)UserRole.Client or (int)UserRole.Freelancer)
        {
            return userRole;
        }

        if (!document.ContractsId.HasValue)
        {
            return (int)ESignerRole.Client;
        }

        var contractParticipants = await context.Set<Contract>()
            .AsNoTracking()
            .Where(contract => contract.ContractsId == document.ContractsId.Value)
            .Select(contract => new
            {
                contract.ClientProfilesId,
                contract.FreelancerProfilesId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (contractParticipants is null)
        {
            return null;
        }

        var isClient = await context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile =>
                    profile.UserId == currentUserId &&
                    profile.ClientProfilesId == contractParticipants.ClientProfilesId,
                cancellationToken);

        if (isClient)
        {
            return (int)ESignerRole.Client;
        }

        return contractParticipants.FreelancerProfilesId.HasValue &&
               await context.Set<FreelancerProfile>()
                   .AsNoTracking()
                   .AnyAsync(
                       profile =>
                           profile.UserId == currentUserId &&
                           profile.FreelancerProfilesId == contractParticipants.FreelancerProfilesId.Value,
                       cancellationToken)
            ? (int)ESignerRole.Freelancer
            : null;
    }
}
