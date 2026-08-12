using Domain.Enums.Accounts;
using Domain.Enums.Auditing;

namespace Application.Common.InternalServices.Auditing.Interfaces;

/// <summary>
/// Records notable Client/Freelancer actions during a contract's lifecycle for later Admin
/// review (e.g. during dispute resolution). Callers must only invoke this after the
/// corresponding business operation has actually succeeded, using the already-authenticated
/// actor's user id and resolved role — never a value supplied by the frontend.
/// </summary>
public interface IUserAuditLogService
{
    void Add(
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
        string? relatedEntityType = null);
}
