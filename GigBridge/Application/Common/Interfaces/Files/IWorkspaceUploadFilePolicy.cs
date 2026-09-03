using Application.Common.Models.Files;

namespace Application.Common.Interfaces.Files;

public interface IWorkspaceUploadFilePolicy
{
    Task<ValidatedWorkspaceUploadBatch> ValidateBatchAsync(
        IReadOnlyList<WorkspaceUploadFile> files,
        int maxFiles,
        CancellationToken cancellationToken);
}
