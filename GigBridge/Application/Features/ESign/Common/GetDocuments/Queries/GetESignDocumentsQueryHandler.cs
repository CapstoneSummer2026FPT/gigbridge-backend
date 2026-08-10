using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.GetDocuments.Queries;

public sealed class GetESignDocumentsQueryHandler
    : IRequestHandler<GetESignDocumentsQuery, PaginatedList<ESignDocumentListItemResponse>>
{
    private const string ContractDocumentType = "Contract";
    private readonly IApplicationDbContext _context;

    public GetESignDocumentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ESignDocumentListItemResponse>> Handle(
        GetESignDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        Guid? clientProfileId = null;
        Guid? freelancerProfileId = null;

        if (request.AdminScope)
        {
            var isAdmin = await _context.Set<User>()
                .AsNoTracking()
                .AnyAsync(
                    user => user.UserId == request.UserId && user.Role == (int)UserRole.Admin,
                    cancellationToken);

            if (!isAdmin)
            {
                throw new ForbiddenAccessException("Only admins can view all e-sign documents.");
            }
        }
        else
        {
            clientProfileId = await _context.Set<ClientProfile>()
                .AsNoTracking()
                .Where(profile => profile.UserId == request.UserId)
                .Select(profile => (Guid?)profile.ClientProfilesId)
                .FirstOrDefaultAsync(cancellationToken);
            freelancerProfileId = await _context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Where(profile => profile.UserId == request.UserId)
                .Select(profile => (Guid?)profile.FreelancerProfilesId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!clientProfileId.HasValue && !freelancerProfileId.HasValue)
            {
                return Empty(request);
            }
        }

        var query =
            from document in _context.Set<EsignDocument>().AsNoTracking()
            where document.ContractsId.HasValue
            join contract in _context.Set<Contract>().AsNoTracking()
                on document.ContractsId equals (Guid?)contract.ContractsId
            join clientProfile in _context.Set<ClientProfile>().AsNoTracking()
                on contract.ClientProfilesId equals clientProfile.ClientProfilesId
            join clientUser in _context.Set<User>().AsNoTracking()
                on clientProfile.UserId equals clientUser.UserId
            join freelancerProfile in _context.Set<FreelancerProfile>().AsNoTracking()
                on contract.FreelancerProfilesId equals (Guid?)freelancerProfile.FreelancerProfilesId into freelancerProfiles
            from freelancerProfile in freelancerProfiles.DefaultIfEmpty()
            join freelancerUser in _context.Set<User>().AsNoTracking()
                on (freelancerProfile != null ? (Guid?)freelancerProfile.UserId : null) equals (Guid?)freelancerUser.UserId into freelancerUsers
            from freelancerUser in freelancerUsers.DefaultIfEmpty()
            select new
            {
                Document = document,
                Contract = contract,
                ClientUser = clientUser,
                FreelancerUser = freelancerUser
            };

        if (!request.AdminScope)
        {
            query = query.Where(item =>
                (clientProfileId.HasValue && item.Contract.ClientProfilesId == clientProfileId.Value) ||
                (freelancerProfileId.HasValue &&
                 item.Contract.FreelancerProfilesId.HasValue &&
                 item.Contract.FreelancerProfilesId.Value == freelancerProfileId.Value));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Document.Status == request.Status.Value);
        }

        var search = request.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.Document.DocumentCode.ToLower().Contains(search) ||
                item.Contract.Title.ToLower().Contains(search) ||
                item.ClientUser.FullName.ToLower().Contains(search) ||
                item.ClientUser.Email.ToLower().Contains(search) ||
                (item.FreelancerUser != null &&
                 (item.FreelancerUser.FullName.ToLower().Contains(search) ||
                  item.FreelancerUser.Email.ToLower().Contains(search))));
        }

        var page = request.Page;
        var pageSize = request.PageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.Document.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ESignDocumentListItemResponse(
                item.Document.EsignDocumentsId,
                item.Document.JobPostsId,
                item.Document.ContractsId,
                item.Document.DocumentCode,
                ContractDocumentType,
                item.Contract.Title,
                item.Document.Status,
                request.AdminScope
                    ? null
                    : clientProfileId.HasValue && item.Contract.ClientProfilesId == clientProfileId.Value
                        ? (int)ESignerRole.Client
                        : (int?)ESignerRole.Freelancer,
                _context.Set<EsignSignature>()
                    .Where(signature =>
                        signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                        signature.UserId == request.UserId &&
                        signature.Status == (int)ESignSignatureStatus.Signed)
                    .Select(signature => signature.SignedAt)
                    .FirstOrDefault(),
                !request.AdminScope &&
                (item.Document.Status == (int)ESignDocumentStatus.PendingSignatures ||
                 item.Document.Status == (int)ESignDocumentStatus.PartiallySigned) &&
                !_context.Set<EsignSignature>().Any(signature =>
                    signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                    signature.UserId == request.UserId &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                _context.Set<EsignSignature>().Any(signature =>
                    signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                    signature.SignerRole == (int)ESignerRole.Client &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                _context.Set<EsignSignature>().Any(signature =>
                    signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                    signature.SignerRole == (int)ESignerRole.Freelancer &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                _context.Set<EsignSignature>().Count(signature =>
                    signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                item.Document.FinalizedAt,
                item.Document.ExportedPdfUrl,
                item.Document.FinalizedDocumentContent != null,
                item.Document.FinalizedDocumentFileName,
                item.Document.PdfDocumentContent != null &&
                item.Document.PdfDocumentHash == (item.Document.DocumentHash ?? string.Empty) +
                    (item.Document.ContractsId.HasValue ? ":contract-template-pdf-v2" : ":client-pdf-v2") &&
                item.Document.PdfSignatureCount == _context.Set<EsignSignature>().Count(signature =>
                    signature.EsignDocumentsId == item.Document.EsignDocumentsId &&
                    signature.Status == (int)ESignSignatureStatus.Signed),
                item.Document.CreatedAt,
                item.Document.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ESignDocumentListItemResponse>(items, totalCount, page, pageSize);
    }

    private static PaginatedList<ESignDocumentListItemResponse> Empty(GetESignDocumentsQuery request)
    {
        return new PaginatedList<ESignDocumentListItemResponse>([], 0, request.Page, request.PageSize);
    }
}
