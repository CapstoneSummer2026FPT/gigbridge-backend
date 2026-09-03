namespace Application.Common.Models.Files;

public static class WorkspaceUploadLimits
{
    public const long MaxFileSizeBytes = 10L * 1024 * 1024;
    public const long MaxTotalFileSizeBytes = 100L * 1024 * 1024;
    public const int MaxFilesPerBatch = 5;
    public const long MultipartOverheadAllowanceBytes = 256L * 1024;
}
