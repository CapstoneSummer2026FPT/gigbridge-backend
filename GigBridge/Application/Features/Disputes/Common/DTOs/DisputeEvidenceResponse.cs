namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeEvidenceResponse(
    Guid DisputeEvidenceId,
    Guid? UploadedById,
    string? FileName,
    long? FileSize,
    string? Description,
    DateTime CreatedAt,
    bool IsRequestedByAdmin,
    Guid? RequestGroupId,
    Guid? RequestedByAdminId,
    DateTime? RequestedAt,
    DateTime? Deadline,
    int? RequestTarget,
    bool IsRequestFulfilled,
    Guid? ReviewedByAdminId,
    DateTime? ReviewedAt,
    string? ReviewNote,
    string? UploadedByName,
    string? RequestedByAdminName,
    string? ReviewedByAdminName)
{
    public DisputeEvidenceResponse(
        Guid disputeEvidenceId,
        Guid? uploadedById,
        string? fileName,
        long? fileSize,
        string? description,
        DateTime createdAt,
        bool isRequestedByAdmin,
        Guid? requestGroupId,
        Guid? requestedByAdminId,
        DateTime? requestedAt,
        DateTime? deadline,
        int? requestTarget,
        bool isRequestFulfilled,
        Guid? reviewedByAdminId,
        DateTime? reviewedAt,
        string? reviewNote)
        : this(
            disputeEvidenceId, uploadedById, fileName, fileSize, description, createdAt,
            isRequestedByAdmin, requestGroupId, requestedByAdminId, requestedAt, deadline,
            requestTarget, isRequestFulfilled, reviewedByAdminId, reviewedAt, reviewNote,
            null, null, null)
    {
    }

    public DisputeEvidenceResponse(
        Guid disputeEvidenceId,
        Guid? uploadedById,
        string? fileName,
        long? fileSize,
        string? description,
        DateTime createdAt)
        : this(
            disputeEvidenceId,
            uploadedById,
            fileName,
            fileSize,
            description,
            createdAt,
            false,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }
}
