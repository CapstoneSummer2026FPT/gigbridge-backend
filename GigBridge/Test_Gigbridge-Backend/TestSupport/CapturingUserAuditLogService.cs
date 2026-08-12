using Application.Common.Interfaces.IService;
using Domain.Enums;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class CapturingUserAuditLogService : IUserAuditLogService
{
    public List<CapturedUserAuditLog> Entries { get; } = new();

    public void Add(
        Guid userId,
        UserRole role,
        AuditUserActionType actionType,
        Guid contractId,
        string description,
        Guid? jobPostId = null,
        Guid? milestoneId = null,
        Guid? reportId = null,
        Guid? disputeId = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        Entries.Add(new CapturedUserAuditLog(
            userId, role, actionType, contractId, description,
            jobPostId, milestoneId, reportId, disputeId, relatedEntityId, relatedEntityType));
    }
}

internal sealed record CapturedUserAuditLog(
    Guid UserId,
    UserRole Role,
    AuditUserActionType ActionType,
    Guid ContractId,
    string Description,
    Guid? JobPostId,
    Guid? MilestoneId,
    Guid? ReportId,
    Guid? DisputeId,
    Guid? RelatedEntityId,
    string? RelatedEntityType);
