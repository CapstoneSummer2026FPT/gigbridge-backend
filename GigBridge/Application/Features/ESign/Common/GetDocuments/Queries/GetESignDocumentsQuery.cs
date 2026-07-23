using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.GetDocuments.Queries;

public sealed record GetESignDocumentsQuery(
    Guid UserId,
    bool AdminScope = false,
    int Page = 1,
    int PageSize = 10,
    int? Status = null,
    string? Q = null) : IRequest<PaginatedList<ESignDocumentListItemResponse>>;
