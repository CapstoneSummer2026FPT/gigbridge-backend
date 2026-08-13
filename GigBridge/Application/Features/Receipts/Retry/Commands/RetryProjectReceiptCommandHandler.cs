using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Interfaces.Time;
using Application.Features.Receipts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.Retry.Commands;

public sealed class RetryProjectReceiptCommandHandler
    : IRequestHandler<RetryProjectReceiptCommand, ProjectReceiptSummaryResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public RetryProjectReceiptCommandHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<ProjectReceiptSummaryResponse> Handle(
        RetryProjectReceiptCommand request,
        CancellationToken cancellationToken)
    {
        var receipt = await _context.Set<ProjectReceipt>()
            .Include(item => item.Contract)
            .FirstOrDefaultAsync(item => item.ProjectReceiptId == request.ReceiptId, cancellationToken)
            ?? throw new NotFoundException("Receipt does not exist.");
        if (receipt.OwnerUserId != request.UserId)
        {
            throw new ForbiddenAccessException("You cannot retry another user's receipt.");
        }

        var now = _clock.UtcNow;
        if (receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Failed)
        {
            receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Pending;
            receipt.GenerationAttemptCount = 0;
            receipt.GenerationLastError = null;
            receipt.GenerationLeaseToken = null;
            receipt.GenerationLeaseExpiresAt = null;
            receipt.NextGenerationAttemptAt = now;
        }
        if (receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
            receipt.EmailStatus == (int)ProjectReceiptEmailStatus.Failed)
        {
            receipt.EmailStatus = (int)ProjectReceiptEmailStatus.Pending;
            receipt.EmailAttemptCount = 0;
            receipt.EmailLastError = null;
            receipt.NextEmailAttemptAt = now;
        }
        receipt.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return ProjectReceiptResponseMapper.ToSummary(receipt);
    }
}
