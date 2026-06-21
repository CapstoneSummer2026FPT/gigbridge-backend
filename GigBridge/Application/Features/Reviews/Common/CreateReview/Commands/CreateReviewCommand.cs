using Application.Features.Reviews.Common.DTOs;
using MediatR;

namespace Application.Features.Reviews.Common.CreateReview.Commands;

public record CreateReviewCommand(Guid UserId, CreateReviewRequest Request) : IRequest<ReviewDto>;
