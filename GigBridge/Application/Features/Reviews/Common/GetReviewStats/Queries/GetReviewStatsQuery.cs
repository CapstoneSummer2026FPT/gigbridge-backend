using Application.Features.Reviews.Common.DTOs;
using MediatR;

namespace Application.Features.Reviews.Common.GetReviewStats.Queries;

public record GetReviewStatsQuery(Guid UserId) : IRequest<ReviewStatsDto>;
