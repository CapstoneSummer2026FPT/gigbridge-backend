namespace Application.Common.Interfaces.Files;

public sealed record WorkspaceUploadFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
