using Application.Features.ESign.Common.DTOs;
using MediatR;
using System;

namespace Application.Features.ESign.Common.GetDocumentByContract.Queries;

public sealed record GetESignDocumentByContractQuery(
    Guid ContractId,
    Guid UserId) : IRequest<ESignDocumentStatusResponse>;
