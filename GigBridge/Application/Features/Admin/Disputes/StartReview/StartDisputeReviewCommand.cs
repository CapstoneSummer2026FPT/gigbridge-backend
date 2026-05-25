using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Disputes.StartReview;

public sealed record StartDisputeReviewCommand(
    Guid DisputeId,
    DisputeReviewRequestDto Request,
    AdminActorDto Actor) : IRequest<DisputeDetailDto>;
