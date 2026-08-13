using Application.Common.Models;
using Application.Features.Receipts.Common.DTOs;
using MediatR;

namespace Application.Features.Receipts.GetMine.Queries;

public sealed record GetMyProjectReceiptsQuery(Guid UserId, int Page = 1, int PageSize = 10)
    : IRequest<PaginatedList<ProjectReceiptSummaryResponse>>;
