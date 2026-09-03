using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.GetDocumentStatusByContract.Queries;

public sealed record GetESignDocumentStatusByContractQuery(Guid ContractId, Guid UserId)
    : IRequest<ESignDocumentLightweightStatusResponse>;
