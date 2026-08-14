using Application.Features.Receipts.Common.DTOs;
using MediatR;

namespace Application.Features.Receipts.Retry.Commands;

public sealed record RetryProjectReceiptCommand(Guid ReceiptId, Guid UserId)
    : IRequest<ProjectReceiptSummaryResponse>;
