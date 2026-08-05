using Application.Common.Models;
using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Queries.GetMyEloAppeals;

public sealed record GetMyEloAppealsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    int? Status = null) : IRequest<PaginatedList<EloAppealDto>>;
