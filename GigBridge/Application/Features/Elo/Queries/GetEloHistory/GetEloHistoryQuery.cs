using Application.Common.Models;
using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Queries.GetEloHistory;

public sealed record GetEloHistoryQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    string? Filter = null) : IRequest<PaginatedList<EloTransactionDto>>;
