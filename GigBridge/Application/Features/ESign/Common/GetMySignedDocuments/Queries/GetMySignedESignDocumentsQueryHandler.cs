using Application.Common.InternalServices.ESign.Services;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.ESign.Common.GetMySignedDocuments.Queries;

public sealed class GetMySignedESignDocumentsQueryHandler
    : IRequestHandler<GetMySignedESignDocumentsQuery, PaginatedList<ESignDocumentListItemResponse>>
{
    private const string JobDocumentType = "JobPost";
    private const string ContractDocumentType = "Contract";

    private readonly IApplicationDbContext _context;

    public GetMySignedESignDocumentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ESignDocumentListItemResponse>> Handle(
        GetMySignedESignDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = request.PageSize is < 1 or > 100 ? 10 : request.PageSize;
        var documentType = NormalizeDocumentType(request.DocumentType);
        var search = request.Q?.Trim().ToLowerInvariant();

        var clientProfileId = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(profile => profile.UserId == request.UserId)
            .Select(profile => (Guid?)profile.ClientProfilesId)
            .FirstOrDefaultAsync(cancellationToken);

        var freelancerProfileId = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .Where(profile => profile.UserId == request.UserId)
            .Select(profile => (Guid?)profile.FreelancerProfilesId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!clientProfileId.HasValue && !freelancerProfileId.HasValue)
        {
            return new PaginatedList<ESignDocumentListItemResponse>(new List<ESignDocumentListItemResponse>(), 0, page, pageSize);
        }

        var signedByCurrentUser = _context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature =>
                signature.UserId == request.UserId &&
                signature.Status == (int)ESignSignatureStatus.Signed);

        var query =
            from currentSignature in signedByCurrentUser
            join document in _context.Set<EsignDocument>().AsNoTracking()
                on currentSignature.EsignDocumentsId equals document.EsignDocumentsId
            join jobPost in _context.Set<JobPost>().AsNoTracking()
                on document.JobPostsId equals jobPost.JobPostsId
            join contract in _context.Set<Contract>().AsNoTracking()
                on document.ContractsId equals contract.ContractsId into contractJoin
            from contract in contractJoin.DefaultIfEmpty()
            where
                (!document.ContractsId.HasValue &&
                    clientProfileId.HasValue &&
                    jobPost.ClientProfilesId == clientProfileId.Value) ||
                (document.ContractsId.HasValue &&
                    contract != null &&
                    ((clientProfileId.HasValue && contract.ClientProfilesId == clientProfileId.Value) ||
                     (freelancerProfileId.HasValue &&
                        contract.FreelancerProfilesId.HasValue &&
                        contract.FreelancerProfilesId.Value == freelancerProfileId.Value)))
            select new ESignDocumentListItemResponse(
                document.EsignDocumentsId,
                document.JobPostsId,
                document.ContractsId,
                document.DocumentCode,
                document.ContractsId.HasValue ? ContractDocumentType : JobDocumentType,
                document.ContractsId.HasValue && contract != null ? contract.Title : jobPost.Title,
                document.Status,
                currentSignature.SignerRole,
                currentSignature.SignedAt,
                false,
                _context.Set<EsignSignature>().Any(signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.SignerRole == (int)ESignerRole.Client &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                _context.Set<EsignSignature>().Any(signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.SignerRole == (int)ESignerRole.Freelancer &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                _context.Set<EsignSignature>().Count(signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                document.FinalizedAt,
                document.ExportedPdfUrl,
                document.FinalizedDocumentSizeBytes > 0,
                document.FinalizedDocumentFileName,
                document.PdfDocumentSizeBytes > 0 &&
                document.PdfDocumentHash == (document.DocumentHash ?? string.Empty) +
                    (document.ContractsId.HasValue
                        ? ESignPdfArtifactRevision.ContractTemplate
                        : ESignPdfArtifactRevision.ClientRendered) &&
                document.PdfSignatureCount == _context.Set<EsignSignature>().Count(signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                document.CreatedAt,
                document.UpdatedAt);

        if (request.Status.HasValue)
        {
            query = query.Where(document => document.DocumentStatus == request.Status.Value);
        }

        if (documentType is not null)
        {
            query = query.Where(document => document.DocumentType == documentType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(document =>
                document.DocumentCode.ToLower().Contains(search) ||
                document.Title.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(document => document.CurrentUserSignedAt)
            .ThenByDescending(document => document.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<ESignDocumentListItemResponse>(items, totalCount, page, pageSize);
    }

    private static string? NormalizeDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return null;
        }

        return documentType.Trim().ToLowerInvariant() switch
        {
            "job" => JobDocumentType,
            "contract" => ContractDocumentType,
            _ => null
        };
    }
}
