namespace Application.Common.Models.Files;

public sealed class ValidatedWorkspaceUploadFile : IAsyncDisposable
{
    private readonly bool _ownsContent;

    public ValidatedWorkspaceUploadFile(
        Stream content,
        string fileName,
        string contentType,
        long length,
        bool ownsContent)
    {
        Content = content;
        FileName = fileName;
        ContentType = contentType;
        Length = length;
        _ownsContent = ownsContent;
    }

    public Stream Content { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long Length { get; }

    public ValueTask DisposeAsync() =>
        _ownsContent ? Content.DisposeAsync() : ValueTask.CompletedTask;
}
