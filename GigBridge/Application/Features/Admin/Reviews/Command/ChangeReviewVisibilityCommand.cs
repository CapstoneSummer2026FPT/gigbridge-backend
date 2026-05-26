using Application.DTOs.Admin;
using Application.Features.Admin.Reviews.Dto;
using MediatR;

namespace Application.Features.Admin.Reviews.Command;

public sealed record ChangeReviewVisibilityCommand(
    Guid ReviewId,
    ReviewVisibilityRequestDto Request,
    AdminActorDto Actor) : IRequest<ReviewDto>;
