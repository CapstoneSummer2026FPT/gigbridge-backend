using Application.Features.Contracts.Completion.Freelancer.DTOs;
using MediatR;

namespace Application.Features.Contracts.Completion.Freelancer.Commands;

public sealed record ClaimFinalPayoutCommand(
    Guid ContractId,
    Guid UserId) : IRequest<ClaimFinalPayoutResponse>;
