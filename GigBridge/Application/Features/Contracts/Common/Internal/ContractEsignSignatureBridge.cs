using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Services;
using Domain.Entities;
using Domain.Enums.ESign;
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
        ESignDocumentRevision.Advance(document, now);
        await ESignDocumentRevision.EnqueueAsync(
            context,
            document,
            now,
            cancellationToken);

        return document;
    }

}

internal sealed record ContractEsignReadiness(
    bool HasClientSignature,
    bool HasFreelancerSignature)
{
    public bool IsFullySigned => HasClientSignature && HasFreelancerSignature;
}
