using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public sealed record ResolveAdminDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    DisputeResolution Resolution,
    string ResolutionNote) : IRequest<AdminDisputeDetailResponse>;
