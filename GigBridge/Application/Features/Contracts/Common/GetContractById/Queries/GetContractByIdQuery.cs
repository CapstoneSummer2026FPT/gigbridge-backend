using System;
using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using MediatR;

namespace Application.Features.Contracts.Common.GetContractById.Queries;

public record GetContractByIdQuery(Guid ContractId, Guid UserId) : IRequest<ContractDetailResponse>;
