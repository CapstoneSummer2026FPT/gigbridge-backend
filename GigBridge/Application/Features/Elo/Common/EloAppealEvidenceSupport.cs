using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Domain.Entities;

namespace Application.Features.Elo.Common;

public sealed record EloAppealFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string? Description);

/// <summary>
/// File upload rules + persistence for Elo appeal evidence. Mirrors
/// <see cref="Application.Features.Disputes.Common.Internal.DisputeEvidenceSupport"/>
/// except files are optional (an appeal needs only a text reason), and files are
/// stored as public URLs via <see cref="IMediaService.UploadFileAsync"/>.
/// </summary>
public static class EloAppealEvidenceSupport
{
    public const int MaxFilesPerRequest = 5;
    public const long MaxFileSizeBytes = 100 * 1024 * 1024;

    /// <summary>Allows zero files (text-only appeal) but caps the batch and per-file limits.</summary>
    public static void ValidateOptionalBatch(IReadOnlyCollection<EloAppealFile> files)
    {
        if (files.Count > MaxFilesPerRequest)
            throw new BadRequestException($"No more than {MaxFilesPerRequest} evidence files can be uploaded at a time.");

        foreach (var file in files)
            ValidateFile(file);
    }

    public static async Task<EloPointAppealEvidence> UploadAsync(
        IMediaService mediaService,
        EloAppealFile file,
        Guid appealId,
        Guid uploadedById,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        var safeFileName = ValidateFile(file);
        var fileUrl = await mediaService.UploadFileAsync(
            file.Content,
            safeFileName,
            file.ContentType,
            "elo-appeals",
            cancellationToken);

        return new EloPointAppealEvidence
        {
            EloPointAppealEvidenceId = Guid.NewGuid(),
            EloPointAppealId = appealId,
            UploadedById = uploadedById,
            FileName = safeFileName,
            FileUrl = fileUrl,
            FileSize = file.Length,
            Description = file.Description?.Trim(),
            CreatedAt = uploadedAt
        };
    }

    private static string ValidateFile(EloAppealFile file)
    {
        if (file.Length <= 0)
            throw new BadRequestException("Evidence file is empty.");

        if (file.Length > MaxFileSizeBytes)
            throw new BadRequestException("Evidence file size exceeds the maximum allowed size of 100 MB.");

        if (string.IsNullOrWhiteSpace(file.FileName))
            throw new BadRequestException("Evidence file name is required.");

        var safeFileName = Path.GetFileName(file.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName))
            throw new BadRequestException("Evidence file name is invalid.");

        if (safeFileName.Length > 255)
            throw new BadRequestException("Evidence file name must not exceed 255 characters.");

        return safeFileName;
    }
}
