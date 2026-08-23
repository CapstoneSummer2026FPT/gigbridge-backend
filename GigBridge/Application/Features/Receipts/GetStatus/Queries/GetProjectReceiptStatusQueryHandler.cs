using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Receipts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.GetStatus.Queries;

public sealed class GetProjectReceiptStatusQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProjectReceiptStatusQuery, ProjectReceiptSummaryResponse>
{
    public async Task<ProjectReceiptSummaryResponse> Handle(
        GetProjectReceiptStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ReceiptId.HasValue == request.ContractId.HasValue)
            throw new BadRequestException("Specify exactly one receipt status identifier.");

        return await context.Set<ProjectReceipt>()
            .AsNoTracking()
            .TagWith("Receipt.Status")
            .Where(item => item.OwnerUserId == request.UserId &&
                (!request.ReceiptId.HasValue || item.ProjectReceiptId == request.ReceiptId) &&
                (!request.ContractId.HasValue || item.ContractsId == request.ContractId))
            .Select(item => new ProjectReceiptSummaryResponse(
                item.ProjectReceiptId,
                item.ContractsId,
                item.Contract.Title,
                item.ReceiptNumber,
                ((ProjectReceiptType)item.ReceiptType).ToString(),
                item.IssuedAt,
                ((ProjectReceiptGenerationStatus)item.GenerationStatus).ToString(),
                ((ProjectReceiptEmailStatus)item.EmailStatus).ToString(),
                item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready && item.PdfSizeBytes > 0,
                item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Failed ||
                    item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
                    item.EmailStatus == (int)ProjectReceiptEmailStatus.Failed,
                item.GeneratedAt,
                item.EmailedAt,
                item.Revision))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Receipt does not exist.");
    }
}
