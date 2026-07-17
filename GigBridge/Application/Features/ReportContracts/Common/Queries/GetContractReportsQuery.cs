using Application.Features.ReportContracts.Common.DTOs;
using MediatR;

namespace Application.Features.ReportContracts.Common.Queries;

public sealed record GetContractReportsQuery(
    Guid ContractId,
    Guid UserId) : IRequest<IReadOnlyList<ReportContractListResponse>>;
