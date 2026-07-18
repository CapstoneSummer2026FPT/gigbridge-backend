using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.UpdateStatus.Commands;

public sealed record UpdateAdminDisputeStatusCommand(
    Guid DisputeId,
    Guid AdminId,
    DisputeStatus Status) : IRequest<AdminDisputeDetailResponse>;
