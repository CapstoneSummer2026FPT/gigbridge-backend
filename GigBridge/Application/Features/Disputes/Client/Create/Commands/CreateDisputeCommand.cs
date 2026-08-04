using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Client.Create.Commands;

public sealed record CreateDisputeCommand(Guid UserId, CreateDisputeRequest Request)
    : IRequest<DisputeDto>;
