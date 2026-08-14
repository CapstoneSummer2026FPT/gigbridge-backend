using Application.Features.Receipts.Common.DTOs;
using MediatR;

namespace Application.Features.Receipts.Download.Queries;

public sealed record DownloadProjectReceiptQuery(Guid ReceiptId, Guid UserId)
    : IRequest<ProjectReceiptDownloadResponse>;
