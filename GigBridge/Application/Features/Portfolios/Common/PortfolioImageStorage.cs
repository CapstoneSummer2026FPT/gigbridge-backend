using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Portfolios.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace Application.Features.Portfolios.Common;

internal static class PortfolioImageStorage
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"]
        };

    public static async Task<string> UploadAsync(
        IMediaService mediaService,
        PortfolioImageUpload image,
        Guid freelancerProfileId,
        CancellationToken cancellationToken)
    {
        if (image.FileSize <= 0 || image.FileSize > MaximumFileSize)
        {
            throw new BadRequestException("Portfolio image must be between 1 byte and 5 MB.");
        }

        var safeFileName = Path.GetFileName(image.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !AllowedExtensions.TryGetValue(image.ContentType, out var extensions) ||
            !extensions.Contains(Path.GetExtension(safeFileName), StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Portfolio image must be a JPEG, PNG, or WebP file.");
        }

        await using var bufferedContent = await ReadImageAsync(image.Content, cancellationToken);
        EnsureImageSignature(bufferedContent.GetBuffer(), (int)bufferedContent.Length, image.ContentType);
        bufferedContent.Position = 0;

        return await mediaService.UploadFileAsync(
            bufferedContent,
            safeFileName,
            image.ContentType,
            $"portfolio/{freelancerProfileId}",
            cancellationToken);
    }

    public static async Task TryDeleteAsync(
        IMediaService mediaService,
        string? imageUrl,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        try
        {
            await mediaService.DeleteFileAsync(imageUrl, "portfolio", CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to delete replaced or removed portfolio image from Cloudinary. ImageUrl={ImageUrl}",
                imageUrl);
        }
    }

    private static async Task<MemoryStream> ReadImageAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var output = new MemoryStream();

        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumFileSize)
            {
                await output.DisposeAsync();
                throw new BadRequestException("Portfolio image must be between 1 byte and 5 MB.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (output.Length == 0)
        {
            await output.DisposeAsync();
            throw new BadRequestException("Portfolio image must be between 1 byte and 5 MB.");
        }

        return output;
    }

    private static void EnsureImageSignature(byte[] content, int length, string contentType)
    {
        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => length >= 3 &&
                            content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/png" => length >= 8 &&
                           content.AsSpan(0, 8).SequenceEqual(
                               new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => length >= 12 &&
                            content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                            content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!valid)
        {
            throw new BadRequestException("The uploaded portfolio file content is not a valid image.");
        }
    }
}
