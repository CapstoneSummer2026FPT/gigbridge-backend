using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloUserHistory;

public sealed record GetAdminEloUserHistoryQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    string? Filter = null) : IRequest<PaginatedList<AdminEloTransactionRowDto>>;
