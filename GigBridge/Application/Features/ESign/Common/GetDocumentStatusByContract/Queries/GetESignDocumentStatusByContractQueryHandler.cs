using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.GetDocumentStatusByContract.Queries;

public sealed class GetESignDocumentStatusByContractQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetESignDocumentStatusByContractQuery, ESignDocumentLightweightStatusResponse>
{
    public async Task<ESignDocumentLightweightStatusResponse> Handle(
        GetESignDocumentStatusByContractQuery request,
        CancellationToken cancellationToken)
    {
        var document = await StatusDocumentQuery(context, request.ContractId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("E-sign document does not exist for this contract.");

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            context,
            document,
            request.UserId,
            cancellationToken);

        return await ESignDocumentProjection.ToLightweightStatusResponseAsync(
            context,
            document,
            request.UserId,
            cancellationToken);
    }

    internal static IQueryable<EsignDocument> StatusDocumentQuery(
        IApplicationDbContext context,
        Guid contractId) =>
        context.Set<EsignDocument>()
            .AsNoTracking()
            .TagWith("ESign.Status.Document")
            // Exclude Voided/Expired rows: see GetESignDocumentByContractQueryHandler for why
            // a stale Voided document (from a Cancelled-then-reused Contract) must not
            // masquerade as the current one.
            .Where(item =>
                item.ContractsId == contractId &&
                item.Status != (int)ESignDocumentStatus.Voided &&
                item.Status != (int)ESignDocumentStatus.Expired)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new EsignDocument
            {
                EsignDocumentsId = item.EsignDocumentsId,
                JobPostsId = item.JobPostsId,
                ContractsId = item.ContractsId,
                Status = item.Status,
                ContentRevision = item.ContentRevision,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                ExpiresAt = item.ExpiresAt,
                FinalizedAt = item.FinalizedAt,
                FinalizedDocumentSizeBytes = item.FinalizedDocumentSizeBytes,
                PdfDocumentHash = item.PdfDocumentHash,
                PdfSignatureCount = item.PdfSignatureCount,
                PdfDocumentSizeBytes = item.PdfDocumentSizeBytes
            });
}
