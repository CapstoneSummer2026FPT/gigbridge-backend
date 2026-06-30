using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.GetMySignedDocuments.Queries;

public sealed record GetMySignedESignDocumentsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 10,
    int? Status = null,
    string? DocumentType = null,
    string? Q = null) : IRequest<PaginatedList<ESignDocumentListItemResponse>>;
