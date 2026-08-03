using Application.Common.Interfaces.IService;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class FakeMediaService : IMediaService
{
    private readonly Queue<string> _urls;

    public FakeMediaService(params string[] urls)
    {
        _urls = new Queue<string>(urls.Length == 0
            ? new[] { "https://res.cloudinary.com/gigbridge/signature.png" }
            : urls);
    }

    public List<UploadCall> Uploads { get; } = new();

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, cancellationToken);
        Uploads.Add(new UploadCall(fileName, contentType, folder, memory.ToArray()));

        return _urls.Count == 0
            ? "https://res.cloudinary.com/gigbridge/signature.png"
            : _urls.Dequeue();
    }

    public Task<string> UploadPrivateFileAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
        => UploadFileAsync(fileStream, fileName, contentType, folder, cancellationToken);

    public Task<string> GetPrivateDownloadUrlAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult(storageKey);

    public Task DeletePrivateFileAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public sealed record UploadCall(
        string FileName,
        string ContentType,
        string Folder,
        byte[] Bytes);
}
