namespace Domain.Entities;

public sealed class EsignDocumentContent
{
    public Guid EsignDocumentsId { get; set; }

    public string RenderedHtmlContent { get; set; } = null!;

    public string? ContractSnapshotJson { get; set; }

    public byte[]? FinalizedDocumentContent { get; set; }

    public string? FinalizedDocumentMimeType { get; set; }

    public byte[]? PdfDocumentContent { get; set; }

    public string? PdfDocumentFileName { get; set; }

    public EsignDocument EsignDocument { get; set; } = null!;
}
