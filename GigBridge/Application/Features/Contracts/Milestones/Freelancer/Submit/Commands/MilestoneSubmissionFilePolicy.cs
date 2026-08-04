using System.Globalization;
using System.Text;
using Application.Common.Exceptions;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

internal sealed record ValidatedMilestoneSubmissionFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

internal static class MilestoneSubmissionFilePolicy
{
    internal const long MaxFileSizeBytes = 100 * 1024 * 1024;

    private const int MaxFileNameLength = 255;
    private const int HeaderInspectionLength = 512;

    private static readonly IReadOnlyDictionary<string, AllowedFileType> AllowedFileTypes =
        new Dictionary<string, AllowedFileType>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = Allowed(SignatureKind.Pdf, "application/pdf"),
            [".doc"] = Allowed(SignatureKind.OleCompound, "application/msword"),
            [".docx"] = Allowed(
                SignatureKind.Zip,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            [".xls"] = Allowed(SignatureKind.OleCompound, "application/vnd.ms-excel"),
            [".xlsx"] = Allowed(
                SignatureKind.Zip,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            [".ppt"] = Allowed(SignatureKind.OleCompound, "application/vnd.ms-powerpoint"),
            [".pptx"] = Allowed(
                SignatureKind.Zip,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
            [".txt"] = Allowed(SignatureKind.Text, "text/plain"),
            [".csv"] = Allowed(SignatureKind.Text, "text/csv", "application/csv"),
            [".json"] = Allowed(SignatureKind.Text, "application/json", "text/json"),
            [".zip"] = Allowed(SignatureKind.Zip, "application/zip", "application/x-zip-compressed"),
            [".rar"] = Allowed(
                SignatureKind.Rar,
                "application/vnd.rar",
                "application/x-rar-compressed"),
            [".7z"] = Allowed(SignatureKind.SevenZip, "application/x-7z-compressed"),
            [".tar"] = Allowed(SignatureKind.Tar, "application/x-tar"),
            [".gz"] = Allowed(SignatureKind.Gzip, "application/gzip", "application/x-gzip"),
            [".jpg"] = Allowed(SignatureKind.Jpeg, "image/jpeg"),
            [".jpeg"] = Allowed(SignatureKind.Jpeg, "image/jpeg"),
            [".png"] = Allowed(SignatureKind.Png, "image/png"),
            [".gif"] = Allowed(SignatureKind.Gif, "image/gif"),
            [".webp"] = Allowed(SignatureKind.WebP, "image/webp"),
            [".mp3"] = Allowed(SignatureKind.Mp3, "audio/mpeg", "audio/mp3"),
            [".wav"] = Allowed(SignatureKind.Wav, "audio/wav", "audio/x-wav", "audio/wave"),
            [".mp4"] = Allowed(SignatureKind.Mp4, "video/mp4"),
            [".webm"] = Allowed(SignatureKind.WebM, "video/webm", "audio/webm")
        };

    internal static async Task<ValidatedMilestoneSubmissionFile> ValidateAsync(
        SubmitMilestoneFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new BadRequestException("Milestone file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new BadRequestException("Milestone file size exceeds the maximum allowed size of 100 MB.");
        }

        if (file.Content is null || !file.Content.CanRead)
        {
            throw new BadRequestException("Milestone file content is invalid.");
        }

        var safeFileName = NormalizeFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedFileTypes.TryGetValue(extension, out var allowedFileType))
        {
            throw new BadRequestException("Milestone file type is not supported.");
        }

        var contentType = NormalizeContentType(file.ContentType);
        if (!allowedFileType.ContentTypes.Contains(contentType))
        {
            throw new BadRequestException(
                "Milestone file extension does not match the declared content type.");
        }

        var (header, uploadContent) = await ReadHeaderWithoutConsumingAsync(
            file.Content,
            file.Length,
            cancellationToken);

        if (!MatchesSignature(header.Span, allowedFileType.Signature))
        {
            throw new BadRequestException(
                "Milestone file content does not match its declared file type.");
        }

        return new ValidatedMilestoneSubmissionFile(
            uploadContent,
            safeFileName,
            contentType,
            file.Length);
    }

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BadRequestException("Milestone file name is required.");
        }

        string normalized;
        try
        {
            normalized = fileName.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException("Milestone file name is invalid.");
        }

        normalized = normalized.Trim().Replace('\\', '/');
        var lastSeparator = normalized.LastIndexOf('/');
        if (lastSeparator >= 0)
        {
            normalized = normalized[(lastSeparator + 1)..];
        }

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(IsUnsafeFileNameCharacter(character) ? '_' : character);
        }

        var safeFileName = builder.ToString().Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            safeFileName is "." or ".." ||
            safeFileName.Length > MaxFileNameLength)
        {
            throw new BadRequestException(
                $"Milestone file name must be between 1 and {MaxFileNameLength} characters.");
        }

        return safeFileName;
    }

    private static bool IsUnsafeFileNameCharacter(char character)
    {
        var category = char.GetUnicodeCategory(character);
        return char.IsControl(character) ||
               category is UnicodeCategory.Format or
                   UnicodeCategory.Surrogate or
                   UnicodeCategory.PrivateUse or
                   UnicodeCategory.OtherNotAssigned ||
               character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new BadRequestException("Milestone file content type is required.");
        }

        var separatorIndex = contentType.IndexOf(';');
        var normalized = (separatorIndex >= 0
                ? contentType[..separatorIndex]
                : contentType)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BadRequestException("Milestone file content type is required.");
        }

        return normalized;
    }

    private static async Task<(ReadOnlyMemory<byte> Header, Stream UploadContent)>
        ReadHeaderWithoutConsumingAsync(
            Stream content,
            long declaredLength,
            CancellationToken cancellationToken)
    {
        var headerBuffer = new byte[HeaderInspectionLength];
        var originalPosition = 0L;

        if (content.CanSeek)
        {
            originalPosition = content.Position;
            if (content.Length - originalPosition != declaredLength)
            {
                throw new BadRequestException(
                    "Milestone file length does not match the uploaded content.");
            }
        }

        var bytesRead = 0;
        try
        {
            while (bytesRead < headerBuffer.Length)
            {
                var read = await content.ReadAsync(
                    headerBuffer.AsMemory(bytesRead, headerBuffer.Length - bytesRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }
        }
        finally
        {
            if (content.CanSeek)
            {
                content.Position = originalPosition;
            }
        }

        if (bytesRead == 0)
        {
            throw new BadRequestException("Milestone file is empty.");
        }

        var header = headerBuffer.AsMemory(0, bytesRead);
        var uploadContent = content.CanSeek
            ? content
            : new PrefixReplayStream(header.ToArray(), content);

        return (header, uploadContent);
    }

    private static bool MatchesSignature(ReadOnlySpan<byte> header, SignatureKind signature)
    {
        return signature switch
        {
            SignatureKind.Text => !header.Contains((byte)0),
            SignatureKind.Pdf => StartsWith(header, "%PDF-"u8),
            SignatureKind.Zip =>
                StartsWith(header, [0x50, 0x4B, 0x03, 0x04]) ||
                StartsWith(header, [0x50, 0x4B, 0x05, 0x06]) ||
                StartsWith(header, [0x50, 0x4B, 0x07, 0x08]),
            SignatureKind.OleCompound =>
                StartsWith(header, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
            SignatureKind.Rar =>
                StartsWith(header, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00]) ||
                StartsWith(header, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00]),
            SignatureKind.SevenZip =>
                StartsWith(header, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]),
            SignatureKind.Tar =>
                header.Length >= 262 && header.Slice(257, 5).SequenceEqual("ustar"u8),
            SignatureKind.Gzip => StartsWith(header, [0x1F, 0x8B]),
            SignatureKind.Jpeg => StartsWith(header, [0xFF, 0xD8, 0xFF]),
            SignatureKind.Png =>
                StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            SignatureKind.Gif =>
                StartsWith(header, "GIF87a"u8) || StartsWith(header, "GIF89a"u8),
            SignatureKind.WebP =>
                header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header.Slice(8, 4).SequenceEqual("WEBP"u8),
            SignatureKind.Mp3 =>
                StartsWith(header, "ID3"u8) ||
                (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0),
            SignatureKind.Wav =>
                header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header.Slice(8, 4).SequenceEqual("WAVE"u8),
            SignatureKind.Mp4 =>
                header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8),
            SignatureKind.WebM =>
                StartsWith(header, [0x1A, 0x45, 0xDF, 0xA3]),
            _ => false
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);

    private static AllowedFileType Allowed(
        SignatureKind signature,
        params string[] contentTypes) =>
        new(
            new HashSet<string>(contentTypes, StringComparer.OrdinalIgnoreCase),
            signature);

    private sealed record AllowedFileType(
        IReadOnlySet<string> ContentTypes,
        SignatureKind Signature);

    private enum SignatureKind
    {
        Text,
        Pdf,
        Zip,
        OleCompound,
        Rar,
        SevenZip,
        Tar,
        Gzip,
        Jpeg,
        Png,
        Gif,
        WebP,
        Mp3,
        Wav,
        Mp4,
        WebM
    }

    private sealed class PrefixReplayStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly Stream _inner;
        private int _prefixPosition;

        public PrefixReplayStream(byte[] prefix, Stream inner)
        {
            _prefix = prefix;
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_prefixPosition < _prefix.Length)
            {
                var count = Math.Min(buffer.Length, _prefix.Length - _prefixPosition);
                _prefix.AsSpan(_prefixPosition, count).CopyTo(buffer);
                _prefixPosition += count;
                return count;
            }

            return _inner.Read(buffer);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_prefixPosition < _prefix.Length)
            {
                var count = Math.Min(buffer.Length, _prefix.Length - _prefixPosition);
                _prefix.AsMemory(_prefixPosition, count).CopyTo(buffer);
                _prefixPosition += count;
                return count;
            }

            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
