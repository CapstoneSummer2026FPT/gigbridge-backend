using System.Globalization;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces.Files;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Infrastructure.Adapters.Files;

internal sealed class WorkspaceUploadFilePolicy : IWorkspaceUploadFilePolicy
{
    private const int MaxFileNameLength = 255;
    private const int HeaderInspectionLength = 512;
    private const int MaxArchiveEntries = 10_000;
    private const int MaxArchiveDepth = 3;
    private const long MaxArchiveExpandedBytes = 500L * 1024 * 1024;
    private const int MaxArchiveCompressionRatio = 100;

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
            [".zip"] = Allowed(
                SignatureKind.Zip,
                "application/zip",
                "application/x-zip-compressed",
                "application/octet-stream"),
            [".rar"] = Allowed(
                SignatureKind.Rar,
                "application/vnd.rar",
                "application/x-rar-compressed",
                "application/octet-stream"),
            [".7z"] = Allowed(
                SignatureKind.SevenZip,
                "application/x-7z-compressed",
                "application/octet-stream"),
            [".tar"] = Allowed(
                SignatureKind.Tar,
                "application/x-tar",
                "application/octet-stream"),
            [".gz"] = Allowed(
                SignatureKind.Gzip,
                "application/gzip",
                "application/x-gzip",
                "application/octet-stream"),
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

    private static readonly IReadOnlySet<string> ArchiveExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz"
        };

    private static readonly IReadOnlySet<string> BlockedArchiveEntryExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".com", ".dll", ".scr", ".cpl", ".msi", ".msp", ".mst",
            ".sys", ".drv", ".ocx", ".bat", ".cmd", ".ps1", ".psm1", ".psd1",
            ".vbs", ".vbe", ".wsf", ".wsh", ".hta", ".sh", ".bash", ".zsh",
            ".fish", ".command", ".jar", ".app", ".apk", ".deb", ".rpm", ".dmg",
            ".pkg", ".lnk", ".scf", ".reg", ".inf", ".gadget", ".application"
        };

    public async Task<ValidatedWorkspaceUploadBatch> ValidateBatchAsync(
        IReadOnlyList<WorkspaceUploadFile> files,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        if (maxFiles is < 1 or > WorkspaceUploadLimits.MaxFilesPerBatch)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles));
        }

        if (files.Count > maxFiles)
        {
            throw new BadRequestException($"A file upload may contain at most {maxFiles} files.");
        }

        var totalLength = 0L;
        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                throw new BadRequestException("Uploaded files must not be empty.");
            }

            if (file.Length > WorkspaceUploadLimits.MaxFileSizeBytes)
            {
                throw new BadRequestException("Each uploaded file must not exceed 100 MB.");
            }

            if (totalLength > WorkspaceUploadLimits.MaxTotalFileSizeBytes - file.Length)
            {
                throw new BadRequestException("The total uploaded file size must not exceed 100 MB.");
            }

            totalLength += file.Length;
        }

        var validatedFiles = new List<ValidatedWorkspaceUploadFile>(files.Count);
        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in files)
            {
                var validated = await ValidateFileAsync(file, cancellationToken);
                if (!normalizedNames.Add(validated.FileName))
                {
                    await validated.DisposeAsync();
                    throw new BadRequestException("Uploaded file names must be unique within a batch.");
                }

                validatedFiles.Add(validated);
            }

            return new ValidatedWorkspaceUploadBatch(validatedFiles);
        }
        catch
        {
            foreach (var validatedFile in validatedFiles)
            {
                await validatedFile.DisposeAsync();
            }

            throw;
        }
    }

    private static async Task<ValidatedWorkspaceUploadFile> ValidateFileAsync(
        WorkspaceUploadFile file,
        CancellationToken cancellationToken)
    {
        if (file.Content is null || !file.Content.CanRead)
        {
            throw new BadRequestException("Uploaded file content is invalid.");
        }

        var safeFileName = NormalizeFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedFileTypes.TryGetValue(extension, out var allowedFileType))
        {
            throw new BadRequestException("Uploaded file type is not supported.");
        }

        var contentType = NormalizeContentType(file.ContentType);
        if (!allowedFileType.ContentTypes.Contains(contentType))
        {
            throw new BadRequestException(
                "Uploaded file extension does not match the declared content type.");
        }

        var prepared = await PrepareSeekableContentAsync(
            file.Content,
            file.Length,
            cancellationToken);

        try
        {
            var header = await ReadHeaderAsync(prepared.Content, cancellationToken);
            if (HasExecutableSignature(header.Span))
            {
                throw new BadRequestException("Uploaded file contains executable content.");
            }

            if (!MatchesSignature(header.Span, allowedFileType.Signature))
            {
                throw new BadRequestException(
                    "Uploaded file content does not match its declared file type.");
            }

            prepared.Content.Position = prepared.StartPosition;
            if (ArchiveExtensions.Contains(extension) ||
                allowedFileType.Signature == SignatureKind.Zip)
            {
                await InspectArchiveAsync(
                    prepared.Content,
                    file.Length,
                    cancellationToken);
                prepared.Content.Position = prepared.StartPosition;
            }

            return new ValidatedWorkspaceUploadFile(
                prepared.Content,
                safeFileName,
                contentType,
                file.Length,
                prepared.OwnsContent);
        }
        catch
        {
            if (prepared.OwnsContent)
            {
                await prepared.Content.DisposeAsync();
            }

            throw;
        }
    }

    private static async Task<PreparedContent> PrepareSeekableContentAsync(
        Stream content,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            var startPosition = content.Position;
            if (content.Length - startPosition != declaredLength)
            {
                throw new BadRequestException(
                    "Uploaded file length does not match its content.");
            }

            return new PreparedContent(content, startPosition, false);
        }

        var temporaryContent = CreateTemporaryFileStream();
        try
        {
            var copiedLength = await CopyWithLimitAsync(
                content,
                temporaryContent,
                WorkspaceUploadLimits.MaxFileSizeBytes,
                cancellationToken);
            if (copiedLength != declaredLength)
            {
                throw new BadRequestException(
                    "Uploaded file length does not match its content.");
            }

            temporaryContent.Position = 0;
            return new PreparedContent(temporaryContent, 0, true);
        }
        catch
        {
            await temporaryContent.DisposeAsync();
            throw;
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadHeaderAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var originalPosition = content.Position;
        var buffer = new byte[HeaderInspectionLength];
        var bytesRead = 0;

        while (bytesRead < buffer.Length)
        {
            var read = await content.ReadAsync(
                buffer.AsMemory(bytesRead, buffer.Length - bytesRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        content.Position = originalPosition;
        if (bytesRead == 0)
        {
            throw new BadRequestException("Uploaded files must not be empty.");
        }

        return buffer.AsMemory(0, bytesRead);
    }

    private static async Task InspectArchiveAsync(
        Stream content,
        long compressedLength,
        CancellationToken cancellationToken)
    {
        var context = new ArchiveInspectionContext(compressedLength);
        try
        {
            await InspectArchiveLevelAsync(content, 1, context, cancellationToken);
            if (context.TotalExpandedBytes > compressedLength * MaxArchiveCompressionRatio)
            {
                throw new BadRequestException(
                    $"Archive compression ratio must not exceed {MaxArchiveCompressionRatio}:1.");
            }
        }
        catch (BadRequestException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
                InvalidFormatException or
                SharpCompress.Common.CryptographicException or
                System.Security.Cryptography.CryptographicException or
                IOException or
                NotSupportedException or
                ArgumentException)
        {
            throw new BadRequestException(
                "Archive is corrupted, encrypted, or cannot be safely inspected.");
        }
    }

    private static async Task InspectArchiveLevelAsync(
        Stream archiveContent,
        int depth,
        ArchiveInspectionContext context,
        CancellationToken cancellationToken)
    {
        if (depth > MaxArchiveDepth)
        {
            throw new BadRequestException(
                $"Nested archives must not exceed {MaxArchiveDepth} levels.");
        }

        using var reader = ReaderFactory.OpenReader(
            archiveContent,
            new ReaderOptions { LeaveStreamOpen = true });

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.EntryCount++;
            if (context.EntryCount > MaxArchiveEntries)
            {
                throw new BadRequestException(
                    $"Archives may contain at most {MaxArchiveEntries} entries.");
            }

            var entry = reader.Entry;
            ValidateArchiveEntryPath(entry.Key);
            if (entry.IsEncrypted)
            {
                throw new BadRequestException("Encrypted archives are not allowed.");
            }

            if (entry.IsDirectory)
            {
                continue;
            }

            var entryName = entry.Key ?? string.Empty;
            if (BlockedArchiveEntryExtensions.Contains(Path.GetExtension(entryName)))
            {
                throw new BadRequestException(
                    $"Archive entry '{entryName}' is an executable or command file.");
            }

            using var entryStream = reader.OpenEntryStream();
            await InspectArchiveEntryContentAsync(
                entryStream,
                entryName,
                depth,
                context,
                cancellationToken);
        }
    }

    private static async Task InspectArchiveEntryContentAsync(
        Stream entryContent,
        string entryName,
        int currentDepth,
        ArchiveInspectionContext context,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderInspectionLength];
        var headerLength = await ReadAtMostAsync(entryContent, header, cancellationToken);
        context.AddExpandedBytes(headerLength);

        if (HasExecutableSignature(header.AsSpan(0, headerLength)))
        {
            throw new BadRequestException(
                $"Archive entry '{entryName}' contains executable content.");
        }

        var entryExtension = Path.GetExtension(entryName);
        var isNestedArchive =
            ArchiveExtensions.Contains(entryExtension) ||
            LooksLikeArchive(header.AsSpan(0, headerLength));

        if (!isNestedArchive)
        {
            await DrainWithArchiveLimitAsync(entryContent, context, cancellationToken);
            return;
        }

        if (currentDepth >= MaxArchiveDepth)
        {
            throw new BadRequestException(
                $"Nested archives must not exceed {MaxArchiveDepth} levels.");
        }

        await using var nestedArchive = CreateTemporaryFileStream();
        await nestedArchive.WriteAsync(header.AsMemory(0, headerLength), cancellationToken);
        await CopyEntryRemainderAsync(
            entryContent,
            nestedArchive,
            context,
            cancellationToken);
        nestedArchive.Position = 0;

        await InspectArchiveLevelAsync(
            nestedArchive,
            currentDepth + 1,
            context,
            cancellationToken);
    }

    private static void ValidateArchiveEntryPath(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new BadRequestException(
                $"Archive entry path '{key}' is unsafe.");
        }
    }

    private static async Task<int> ReadAtMostAsync(
        Stream content,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = await content.ReadAsync(
                buffer.AsMemory(bytesRead, buffer.Length - bytesRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        return bytesRead;
    }

    private static async Task DrainWithArchiveLimitAsync(
        Stream content,
        ArchiveInspectionContext context,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            context.AddExpandedBytes(read);
        }
    }

    private static async Task CopyEntryRemainderAsync(
        Stream source,
        Stream destination,
        ArchiveInspectionContext context,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            context.AddExpandedBytes(read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<long> CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long limit,
        CancellationToken cancellationToken)
    {
        var total = 0L;
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return total;
            }

            if (total > limit - read)
            {
                throw new BadRequestException("Each uploaded file must not exceed 100 MB.");
            }

            total += read;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static FileStream CreateTemporaryFileStream()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"gigbridge-upload-{Guid.NewGuid():N}.tmp");
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan |
            FileOptions.DeleteOnClose);
    }

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BadRequestException("Uploaded file name is required.");
        }

        string normalized;
        try
        {
            normalized = fileName.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException("Uploaded file name is invalid.");
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
                $"Uploaded file name must be between 1 and {MaxFileNameLength} characters.");
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
            throw new BadRequestException("Uploaded file content type is required.");
        }

        var separatorIndex = contentType.IndexOf(';');
        var normalized = (separatorIndex >= 0
                ? contentType[..separatorIndex]
                : contentType)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BadRequestException("Uploaded file content type is required.");
        }

        return normalized;
    }

    private static bool HasExecutableSignature(ReadOnlySpan<byte> header) =>
        StartsWith(header, [0x4D, 0x5A]) ||
        StartsWith(header, [0x7F, 0x45, 0x4C, 0x46]) ||
        StartsWith(header, [0xCA, 0xFE, 0xBA, 0xBE]) ||
        StartsWith(header, "dex\n"u8) ||
        StartsWith(header, [0xFE, 0xED, 0xFA, 0xCE]) ||
        StartsWith(header, [0xFE, 0xED, 0xFA, 0xCF]) ||
        StartsWith(header, [0xCE, 0xFA, 0xED, 0xFE]) ||
        StartsWith(header, [0xCF, 0xFA, 0xED, 0xFE]);

    private static bool LooksLikeArchive(ReadOnlySpan<byte> header) =>
        MatchesSignature(header, SignatureKind.Zip) ||
        MatchesSignature(header, SignatureKind.Rar) ||
        MatchesSignature(header, SignatureKind.SevenZip) ||
        MatchesSignature(header, SignatureKind.Tar) ||
        MatchesSignature(header, SignatureKind.Gzip);

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

    private sealed record PreparedContent(Stream Content, long StartPosition, bool OwnsContent);

    private sealed record AllowedFileType(
        IReadOnlySet<string> ContentTypes,
        SignatureKind Signature);

    private sealed class ArchiveInspectionContext
    {
        private readonly long _compressedLength;

        internal ArchiveInspectionContext(long compressedLength)
        {
            _compressedLength = compressedLength;
        }

        internal int EntryCount { get; set; }
        internal long TotalExpandedBytes { get; private set; }

        internal void AddExpandedBytes(long bytes)
        {
            if (bytes < 0 || TotalExpandedBytes > MaxArchiveExpandedBytes - bytes)
            {
                throw new BadRequestException(
                    "Archive expanded content must not exceed 500 MB.");
            }

            TotalExpandedBytes += bytes;
            if (TotalExpandedBytes > _compressedLength * MaxArchiveCompressionRatio)
            {
                throw new BadRequestException(
                    $"Archive compression ratio must not exceed {MaxArchiveCompressionRatio}:1.");
            }
        }
    }

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
}
