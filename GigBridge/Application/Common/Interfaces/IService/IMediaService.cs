using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IService;

public interface IMediaService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);
    Task<string> UploadPrivateFileAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
    Task<string> GetPrivateDownloadUrlAsync(string storageKey, string contentType, CancellationToken cancellationToken = default);
    Task DeletePrivateFileAsync(string storageKey, string contentType, CancellationToken cancellationToken = default);
}
