using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;

namespace Application.Common.Services;

public static partial class SignatureImageStorage
{
    private const string SignatureFolder = "esign/signatures";

    public static async Task<string> UploadSignatureImageAsync(
        IMediaService mediaService,
        string? signatureImageDataUri,
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var parsed = ParseImageDataUri(signatureImageDataUri);
        var extension = ToFileExtension(parsed.ContentType);
        var fileName = $"signature-{documentId:N}-{userId:N}.{extension}";

        await using var stream = new MemoryStream(parsed.Bytes);
        return await mediaService.UploadFileAsync(
            stream,
            fileName,
            parsed.ContentType,
            SignatureFolder,
            cancellationToken);
    }

    private static ParsedDataUri ParseImageDataUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException("Signature image is required.");
        }

        var match = ImageDataUriRegex().Match(value.Trim());
        if (!match.Success)
        {
            throw new BadRequestException("Signature image must be an image data URI.");
        }

        var base64 = match.Groups["data"].Value;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new BadRequestException("Signature image data is not valid base64.");
        }

        if (bytes.Length == 0)
        {
            throw new BadRequestException("Signature image data is empty.");
        }

        return new ParsedDataUri(match.Groups["contentType"].Value.ToLowerInvariant(), bytes);
    }

    private static string ToFileExtension(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => "jpg",
            "image/svg+xml" => "svg",
            _ => contentType["image/".Length..].Replace("+", "-", StringComparison.Ordinal)
        };
    }

    [GeneratedRegex(
        @"^data:(?<contentType>image/[a-z0-9.+-]+);base64,(?<data>[a-zA-Z0-9+/=\r\n]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageDataUriRegex();

    private sealed record ParsedDataUri(string ContentType, byte[] Bytes);
}
