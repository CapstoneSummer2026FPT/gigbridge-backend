namespace Application.Common.InternalServices.ESign.Models;

internal sealed record ESignDocumentContentData(
    Guid DocumentId,
    string RenderedHtmlContent,
    string? ContractSnapshotJson);

internal sealed record ESignArtifactData(
    byte[] Content,
    string FileName,
    string MimeType,
    long SizeBytes,
    string ContentHashSha256,
    int ArtifactRevision);
