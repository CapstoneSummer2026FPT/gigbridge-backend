using Application.Features.ReportContracts.Common.DTOs;
using MediatR;

namespace Application.Features.ReportContracts.Common.Queries;

public sealed record GetReportByIdQuery(
    Guid ContractId,
    Guid ReportId,
    Guid UserId) : IRequest<ReportContractResponse>;
