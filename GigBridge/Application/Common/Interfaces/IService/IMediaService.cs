using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IService;

public interface IMediaService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
}