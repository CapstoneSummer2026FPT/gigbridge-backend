using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetEloAppeals;

public sealed record GetEloAppealsQuery(
    int Page = 1,
    int PageSize = 20,
    int? Status = null,
    string? Search = null) : IRequest<PaginatedList<AdminEloAppealRowDto>>;
