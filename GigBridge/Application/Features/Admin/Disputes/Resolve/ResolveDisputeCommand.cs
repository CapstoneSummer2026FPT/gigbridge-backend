using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Disputes.Resolve;

public sealed record ResolveDisputeCommand(
    Guid DisputeId,
    DisputeResolutionRequestDto Request,
    AdminActorDto Actor) : IRequest<DisputeDetailDto>;
