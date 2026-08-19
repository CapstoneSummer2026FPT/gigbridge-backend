using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Receipts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.GetMine.Queries;

public sealed class GetMyProjectReceiptsQueryHandler
    : IRequestHandler<GetMyProjectReceiptsQuery, PaginatedList<ProjectReceiptSummaryResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetMyProjectReceiptsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<ProjectReceiptSummaryResponse>> Handle(
        GetMyProjectReceiptsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = _context.Set<ProjectReceipt>()
            .AsNoTracking()
            .Where(item => item.OwnerUserId == request.UserId);
        var count = await query.CountAsync(cancellationToken);
        var receipts = await query
            .OrderByDescending(item => item.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ProjectReceiptSummaryResponse(
                item.ProjectReceiptId,
                item.ContractsId,
                item.Contract.Title,
                item.ReceiptNumber,
                ((ProjectReceiptType)item.ReceiptType).ToString(),
                item.IssuedAt,
                ((ProjectReceiptGenerationStatus)item.GenerationStatus).ToString(),
                ((ProjectReceiptEmailStatus)item.EmailStatus).ToString(),
                item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
                    item.PdfSizeBytes > 0,
                item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Failed ||
                    item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
                    item.EmailStatus == (int)ProjectReceiptEmailStatus.Failed,
                item.GeneratedAt,
                item.EmailedAt))
            .ToListAsync(cancellationToken);
        return new PaginatedList<ProjectReceiptSummaryResponse>(
            receipts,
            count,
            page,
            pageSize);
    }
}
