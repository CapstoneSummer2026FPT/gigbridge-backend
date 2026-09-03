using Application.Features.Receipts.Common.DTOs;
using MediatR;

namespace Application.Features.Receipts.GetStatus.Queries;

public sealed record GetProjectReceiptStatusQuery(
    Guid UserId,
    Guid? ReceiptId = null,
    Guid? ContractId = null) : IRequest<ProjectReceiptSummaryResponse>;
