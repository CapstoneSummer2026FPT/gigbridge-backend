using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractEsignSignatureBridge
{
    public static async Task<ContractEsignReadiness> ApplyClientJobPostSignatureAndGetReadinessAsync(
        IApplicationDbContext context,
        Contract contract,
        EsignDocument contractDocument,
        DateTime now,
        CancellationToken cancellationToken,
        ESignerRole? pendingSignedRole = null)
    {
        var signedRoles = await context.Set<EsignSignature>()
            .Where(signature =>
                signature.EsignDocumentsId == contractDocument.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed)
            .Select(signature => signature.SignerRole)
            .ToListAsync(cancellationToken);

        if (pendingSignedRole.HasValue &&
            !signedRoles.Contains((int)pendingSignedRole.Value))
        {
            signedRoles.Add((int)pendingSignedRole.Value);
        }

        if (!signedRoles.Contains((int)ESignerRole.Client) &&
            await TryHydrateClientSignatureFromJobPostAsync(
                context,
                contract,
                contractDocument,
                now,
                cancellationToken))
        {
            signedRoles.Add((int)ESignerRole.Client);
        }

        return new ContractEsignReadiness(
            signedRoles.Contains((int)ESignerRole.Client),
            signedRoles.Contains((int)ESignerRole.Freelancer));
    }

    public static async Task<EsignDocument> EnsureFullySignedContractDocumentAsync(
        IApplicationDbContext context,
        Contract contract,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var document = await context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contract.ContractsId &&
                document.Status != (int)ESignDocumentStatus.Voided &&
                document.Status != (int)ESignDocumentStatus.Expired)
            .OrderByDescending(document => document.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            throw new BadRequestException("Contract escrow can only be funded after both parties sign.");
        }

        if (document.Status == (int)ESignDocumentStatus.FullySigned)
        {
            return document;
        }

        var readiness = await ApplyClientJobPostSignatureAndGetReadinessAsync(
            context,
            contract,
            document,
            now,
            cancellationToken);

        if (!readiness.IsFullySigned)
        {
            throw new BadRequestException("Contract escrow can only be funded after both parties sign.");
        }

        document.Status = (int)ESignDocumentStatus.FullySigned;
        document.FinalizedAt ??= now;
        document.UpdatedAt = now;

        return document;
    }

    private static async Task<bool> TryHydrateClientSignatureFromJobPostAsync(
        IApplicationDbContext context,
        Contract contract,
        EsignDocument contractDocument,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var clientUserId = await context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!clientUserId.HasValue)
        {
            return false;
        }

        var signedContractClientSignature = await context.Set<EsignSignature>()
            .FirstOrDefaultAsync(
                signature =>
                    signature.EsignDocumentsId == contractDocument.EsignDocumentsId &&
                    signature.UserId == clientUserId.Value,
                cancellationToken);

        if (signedContractClientSignature is not null &&
            signedContractClientSignature.Status == (int)ESignSignatureStatus.Signed)
        {
            return true;
        }

        var jobPostDocumentId = await context.Set<EsignDocument>()
            .Where(document =>
                document.JobPostsId == contract.JobPostsId &&
                !document.ContractsId.HasValue &&
                document.Status == (int)ESignDocumentStatus.FullySigned)
            .OrderByDescending(document => document.FinalizedAt ?? document.CreatedAt)
            .Select(document => (Guid?)document.EsignDocumentsId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!jobPostDocumentId.HasValue)
        {
            return false;
        }

        var sourceSignature = await context.Set<EsignSignature>()
            .Where(signature =>
                signature.EsignDocumentsId == jobPostDocumentId.Value &&
                signature.UserId == clientUserId.Value &&
                signature.SignerRole == (int)ESignerRole.Client &&
                signature.Status == (int)ESignSignatureStatus.Signed)
            .OrderByDescending(signature => signature.SignedAt ?? signature.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceSignature is null)
        {
            return false;
        }

        var targetSignature = signedContractClientSignature ?? new EsignSignature
        {
            EsignSignaturesId = Guid.NewGuid(),
            EsignDocumentsId = contractDocument.EsignDocumentsId,
            UserId = clientUserId.Value,
            CreatedAt = sourceSignature.CreatedAt
        };

        if (signedContractClientSignature is null)
        {
            context.Set<EsignSignature>().Add(targetSignature);
        }

        targetSignature.SignerRole = (int)ESignerRole.Client;
        targetSignature.SignatureImageUrl = sourceSignature.SignatureImageUrl;
        targetSignature.SignatureWidth = sourceSignature.SignatureWidth;
        targetSignature.SignatureHeight = sourceSignature.SignatureHeight;
        targetSignature.Status = (int)ESignSignatureStatus.Signed;
        targetSignature.SignedAt = sourceSignature.SignedAt ?? now;
        targetSignature.DeclinedAt = null;
        targetSignature.DeclineReason = null;
        targetSignature.IpAddress = sourceSignature.IpAddress;
        targetSignature.UserAgent = sourceSignature.UserAgent;

        return true;
    }
}

internal sealed record ContractEsignReadiness(
    bool HasClientSignature,
    bool HasFreelancerSignature)
{
    public bool IsFullySigned => HasClientSignature && HasFreelancerSignature;
}
