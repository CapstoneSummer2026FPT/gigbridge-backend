using Application.Features.Receipts.Common.DTOs;
using MediatR;

namespace Application.Features.Receipts.Prepare.Commands;

public sealed record PrepareProjectReceiptsCommand(Guid ContractId, Guid UserId)
    : IRequest<ProjectReceiptSummaryResponse>;
