using Application.DTOs.Admin;
using Application.Features.Admin.Disputes.Dto;
using MediatR;

namespace Application.Features.Admin.Disputes.Command;

public sealed record StartDisputeReviewCommand(
    Guid DisputeId,
    DisputeReviewRequestDto Request,
    AdminActorDto Actor) : IRequest<DisputeDetailDto>;
