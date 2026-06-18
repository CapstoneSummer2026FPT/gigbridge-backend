using System;
using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Common.Get.Queries;

public sealed record GetMilestoneByIdQuery(
    Guid MilestoneId,
    Guid UserId) : IRequest<ContractMilestoneResponse>;
