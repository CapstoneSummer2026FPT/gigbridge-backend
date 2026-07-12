using Application.Features.Contracts.Completion.Client.DTOs;
using MediatR;

namespace Application.Features.Contracts.Completion.Client.Commands;

public sealed record EndProjectCommand(
    Guid ContractId,
    Guid UserId) : IRequest<EndProjectResponse>;

