using Application.Features.Reviews.Common.DTOs;
using MediatR;

namespace Application.Features.Reviews.Common.GetReviewsByUser.Queries;

public record GetReviewsByUserQuery(Guid UserId) : IRequest<IEnumerable<ReviewDto>>;
