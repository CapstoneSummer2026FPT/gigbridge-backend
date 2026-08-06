using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
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
            signatures,
            currentUserId,
            cancellationToken);

        return ToResponse(document, signatures, currentUserId, signerRole);
    }

    public static ESignDocumentResponse ToResponse(
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
        var hasCurrentPdf = document.PdfDocumentContent is { Length: > 0 } &&
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
            document.RenderedHtmlContent,
            document.Status,
            document.DocumentHash,
            document.ExpiresAt,
            document.FinalizedAt,
            document.ExportedPdfUrl,
            signerRole,
            canCurrentUserSign,
            document.FinalizedDocumentContent is { Length: > 0 },
            document.FinalizedDocumentFileName,
            hasCurrentPdf,
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

    private static async Task<int?> ResolveSignerRoleAsync(
        IApplicationDbContext context,
        EsignDocument document,
        IReadOnlyList<EsignSignature> signatures,
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

        var existingSignature = signatures.FirstOrDefault(signature => signature.UserId == currentUserId);
        if (existingSignature is not null)
        {
            return existingSignature.SignerRole;
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
