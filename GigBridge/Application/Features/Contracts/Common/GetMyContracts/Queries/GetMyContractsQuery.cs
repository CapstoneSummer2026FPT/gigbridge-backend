using System;
using System.Collections.Generic;
using Application.Features.Contracts.Common.GetMyContracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Common.GetMyContracts.Queries;

public record GetMyContractsQuery(Guid UserId) : IRequest<List<ContractDtoResponse>>;
