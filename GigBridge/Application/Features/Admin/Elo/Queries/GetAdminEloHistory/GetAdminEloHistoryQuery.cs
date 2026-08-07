using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloHistory;

public sealed record GetAdminEloHistoryQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Filter = null) : IRequest<PaginatedList<AdminEloTransactionRowDto>>;
