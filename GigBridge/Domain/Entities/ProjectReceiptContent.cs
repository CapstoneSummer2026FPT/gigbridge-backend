namespace Domain.Entities;

public sealed class ProjectReceiptContent
{
    public Guid ProjectReceiptId { get; set; }

    public string SnapshotJson { get; set; } = string.Empty;

    public string SnapshotHashSha256 { get; set; } = string.Empty;

    public byte[]? PdfContent { get; set; }

    public string? PdfFileName { get; set; }

    public string? PdfContentType { get; set; }

    public string? PdfHashSha256 { get; set; }

    public ProjectReceipt ProjectReceipt { get; set; } = null!;
}
