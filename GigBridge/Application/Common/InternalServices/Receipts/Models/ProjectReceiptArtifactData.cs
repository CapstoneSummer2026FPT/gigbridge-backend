namespace Application.Common.InternalServices.Receipts.Models;

public sealed record ProjectReceiptArtifactData(
    byte[] Content,
    string FileName,
    string MimeType,
    long SizeBytes,
    string ContentHashSha256,
    int ArtifactRevision);

public sealed record ProjectReceiptSnapshotData(string SnapshotJson, string SnapshotHashSha256);
