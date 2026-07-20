using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Admin.Resolve.Commands;

public sealed record ResolveDisputeCommand(
    Guid AdminUserId,
    Guid DisputeId,
    ResolveDisputeRequest Request) : IRequest<AdminDisputeDto>;
