using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces.IService;
using Microsoft.Extensions.Configuration;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Infrastructure.Services.Media;

public class MediaService : IMediaService
{
    private readonly Cloudinary _cloudinary;

    public MediaService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is incomplete or missing in appsettings.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var resourceType = "raw";
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        if (isImage)
        {
            resourceType = "image";
        }
        else if (isVideo)
        {
            resourceType = "video";
        }

        var publicId = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(fileName)}";

        if (resourceType == "image")
        {
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"gigbridge/{folder}",
                PublicId = publicId
            };

            var uploadResult = await _cloudinary.UploadAsync(imageParams, cancellationToken);
            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
        else if (resourceType == "video")
        {
            var videoParams = new VideoUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"gigbridge/{folder}",
                PublicId = publicId
            };

            var uploadResult = await _cloudinary.UploadAsync(videoParams, cancellationToken);
            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
        else
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"gigbridge/{folder}",
                PublicId = publicId
            };

            var uploadResult = await Task.Run(() => _cloudinary.Upload(uploadParams), cancellationToken);
            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
    }
}