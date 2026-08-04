using Application.Features.Contracts.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.Accept.Commands;

public sealed record AcceptContractMilestonesCommand(
    Guid ContractId,
    Guid UserId) : IRequest<ContractWorkflowResponse>;
