using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Receipts.Common.DTOs;

public sealed record ProjectReceiptSummaryResponse(
    Guid ReceiptId,
    Guid ContractId,
    string ProjectTitle,
    string ReceiptNumber,
    string ReceiptType,
    DateTime IssuedAt,
    string GenerationStatus,
    string EmailStatus,
    bool DownloadReady,
    bool CanRetry,
    DateTime? GeneratedAt,
    DateTime? EmailedAt);

public sealed record ProjectReceiptDownloadResponse(
    byte[] Content,
    string FileName,
    string ContentType);

public static class ProjectReceiptResponseMapper
{
    public static ProjectReceiptSummaryResponse ToSummary(ProjectReceipt receipt) => new(
        receipt.ProjectReceiptId,
        receipt.ContractsId,
        receipt.Contract?.Title ?? "Project",
        receipt.ReceiptNumber,
        ((ProjectReceiptType)receipt.ReceiptType).ToString(),
        receipt.IssuedAt,
        ((ProjectReceiptGenerationStatus)receipt.GenerationStatus).ToString(),
        ((ProjectReceiptEmailStatus)receipt.EmailStatus).ToString(),
        receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
            receipt.PdfContent is { Length: > 0 },
        receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Failed ||
            receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
            receipt.EmailStatus == (int)ProjectReceiptEmailStatus.Failed,
        receipt.GeneratedAt,
        receipt.EmailedAt);
}
