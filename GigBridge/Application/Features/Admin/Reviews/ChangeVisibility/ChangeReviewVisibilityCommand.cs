using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Reviews.ChangeVisibility;

public sealed record ChangeReviewVisibilityCommand(
    Guid ReviewId,
    ReviewVisibilityRequestDto Request,
    AdminActorDto Actor) : IRequest<ReviewDto>;
