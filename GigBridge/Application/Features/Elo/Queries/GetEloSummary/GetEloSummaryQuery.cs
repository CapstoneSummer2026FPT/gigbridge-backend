using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Queries.GetEloSummary;

public sealed record GetEloSummaryQuery(Guid UserId) : IRequest<EloSummaryDto>;
