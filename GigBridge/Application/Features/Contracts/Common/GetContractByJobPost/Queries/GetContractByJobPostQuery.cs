using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using MediatR;

namespace Application.Features.Contracts.Common.GetContractByJobPost.Queries;

public record GetContractByJobPostQuery(Guid JobPostId, Guid UserId) : IRequest<ContractDetailResponse>;
