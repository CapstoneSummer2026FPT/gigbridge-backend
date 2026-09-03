namespace Domain.Entities;

public sealed class EsignDocumentArtifact
{
    public Guid EsignDocumentArtifactId { get; set; }

    public Guid EsignDocumentsId { get; set; }

    public int ArtifactType { get; set; }

    public byte[] Content { get; set; } = [];

    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ContentHashSha256 { get; set; } = string.Empty;

    public int ArtifactRevision { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public EsignDocument EsignDocument { get; set; } = null!;
}
