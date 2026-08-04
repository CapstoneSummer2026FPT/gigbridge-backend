using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Domain.Entities;

namespace Application.Features.Disputes.Common.Internal;

public sealed record DisputeEvidenceFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public static class DisputeEvidenceSupport
{
    public const int MaxFilesPerRequest = 5;
    public const long MaxFileSizeBytes = 100 * 1024 * 1024;

    public static void ValidateBatch(IReadOnlyCollection<DisputeEvidenceFile> files)
    {
        if (files.Count == 0)
            throw new BadRequestException("At least one evidence file is required.");

        if (files.Count > MaxFilesPerRequest)
            throw new BadRequestException($"No more than {MaxFilesPerRequest} evidence files can be uploaded at a time.");

        foreach (var file in files)
            ValidateFile(file);
    }

    public static async Task<DisputeEvidence> UploadAsync(
        IMediaService mediaService,
        DisputeEvidenceFile file,
        Guid disputeId,
        Guid uploadedById,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        var safeFileName = ValidateFile(file);
        var fileUrl = await mediaService.UploadFileAsync(
            file.Content,
            safeFileName,
            file.ContentType,
            "disputes",
            cancellationToken);

        return new DisputeEvidence
        {
            DisputeEvidenceId = Guid.NewGuid(),
            DisputesId = disputeId,
            UploadedById = uploadedById,
            FileName = safeFileName,
            FileUrl = fileUrl,
            FileSize = file.Length,
            Description = null,
            CreatedAt = uploadedAt
        };
    }

    private static string ValidateFile(DisputeEvidenceFile file)
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

        if (safeFileName.Length > 500)
            throw new BadRequestException("Evidence file name must not exceed 500 characters.");

        return safeFileName;
    }
}
