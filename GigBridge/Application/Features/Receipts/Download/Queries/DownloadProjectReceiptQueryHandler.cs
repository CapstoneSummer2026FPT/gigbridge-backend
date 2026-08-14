using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Receipts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.Download.Queries;

public sealed class DownloadProjectReceiptQueryHandler
    : IRequestHandler<DownloadProjectReceiptQuery, ProjectReceiptDownloadResponse>
{
    private readonly IApplicationDbContext _context;

    public DownloadProjectReceiptQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProjectReceiptDownloadResponse> Handle(
        DownloadProjectReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var receipt = await _context.Set<ProjectReceipt>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProjectReceiptId == request.ReceiptId, cancellationToken)
            ?? throw new NotFoundException("Receipt does not exist.");
        if (receipt.OwnerUserId != request.UserId)
        {
            throw new ForbiddenAccessException("You cannot download another user's receipt.");
        }
        if (receipt.GenerationStatus != (int)ProjectReceiptGenerationStatus.Ready ||
            receipt.PdfContent is not { Length: > 0 })
        {
            throw new ConflictException("The receipt PDF is still being prepared.");
        }
        return new ProjectReceiptDownloadResponse(
            receipt.PdfContent,
            receipt.PdfFileName ?? $"GigBridge-{receipt.ReceiptNumber}.pdf",
            receipt.PdfContentType ?? "application/pdf");
    }
}
