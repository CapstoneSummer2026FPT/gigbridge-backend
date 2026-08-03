using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Media;

public class MediaService : IMediaService
{
    private const string UploadFailureMessage = "Media upload failed. Verify Cloudinary configuration and try again.";

    private readonly Cloudinary _cloudinary;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        IOptions<CloudinaryOptions> options,
        ILogger<MediaService> logger)
    {
        var cloudinaryOptions = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(cloudinaryOptions.CloudName) ||
            string.IsNullOrWhiteSpace(cloudinaryOptions.ApiKey) ||
            string.IsNullOrWhiteSpace(cloudinaryOptions.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is incomplete or missing.");
        }

        var account = new Account(
            cloudinaryOptions.CloudName,
            cloudinaryOptions.ApiKey,
            cloudinaryOptions.ApiSecret);
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

        try
        {
            if (resourceType == "image")
            {
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, fileStream),
                    Folder = $"gigbridge/{folder}",
                    PublicId = publicId
                };

                var uploadResult = await _cloudinary.UploadAsync(imageParams, cancellationToken);
                return GetSecureUrl(uploadResult, resourceType);
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
                return GetSecureUrl(uploadResult, resourceType);
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
                return GetSecureUrl(uploadResult, resourceType);
            }
        }
        catch (Exception exception) when (exception is not ExternalServiceException &&
                                         exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Cloudinary {ResourceType} upload failed before a response was returned.",
                resourceType);
            throw new ExternalServiceException(UploadFailureMessage, exception);
        }
    }

    public async Task<string> UploadPrivateFileAsync(
        Stream fileStream, string fileName, string contentType, string folder,
        CancellationToken cancellationToken = default)
    {
        var resourceType = ResolveResourceType(contentType);
        var publicId = $"gigbridge/{folder}/{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(fileName)}";
        try
        {
            UploadResult result;
            if (resourceType == "image")
                result = await _cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription(fileName, fileStream), PublicId = publicId, Type = "authenticated" }, cancellationToken);
            else
                result = await Task.Run(() => _cloudinary.Upload(new RawUploadParams { File = new FileDescription(fileName, fileStream), PublicId = publicId, Type = "authenticated" }), cancellationToken);
            if (result.Error is not null) throw new ExternalServiceException(UploadFailureMessage);
            return result.PublicId;
        }
        catch (Exception exception) when (exception is not ExternalServiceException && exception is not OperationCanceledException)
        { throw new ExternalServiceException(UploadFailureMessage, exception); }
    }

    public Task<string> GetPrivateDownloadUrlAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resourceType = ResolveResourceType(contentType);
        var url = _cloudinary.DownloadPrivate(
            storageKey,
            attachment: true,
            format: null,
            type: "authenticated",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            resourceType: resourceType,
            transformation: null,
            targetFilename: null);
        return Task.FromResult(url);
    }

    public async Task DeletePrivateFileAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resourceType = ResolveResourceType(contentType) == "image" ? ResourceType.Image : ResourceType.Raw;
        await _cloudinary.DestroyAsync(new DeletionParams(storageKey) { ResourceType = resourceType, Type = "authenticated" });
    }

    private static string ResolveResourceType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "raw";

    private string GetSecureUrl(UploadResult uploadResult, string resourceType)
    {
        if (uploadResult.Error is not null)
        {
            _logger.LogWarning(
                "Cloudinary {ResourceType} upload failed: {ErrorMessage}",
                resourceType,
                uploadResult.Error.Message);
            throw new ExternalServiceException(UploadFailureMessage);
        }

        var secureUrl = uploadResult.SecureUrl?.ToString();
        if (string.IsNullOrWhiteSpace(secureUrl))
        {
            _logger.LogWarning(
                "Cloudinary {ResourceType} upload completed without a secure URL.",
                resourceType);
            throw new ExternalServiceException(UploadFailureMessage);
        }

        return secureUrl;
    }
}
