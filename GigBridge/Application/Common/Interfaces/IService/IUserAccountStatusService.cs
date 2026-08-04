using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces.IService;

public interface IUserAccountStatusService
{
    Task<AccountEnforcementResult> ApplyViolationAsync(
        User user,
        AccountViolationSource source,
        UserViolationType violationType,
        string reason,
        string? description,
        Guid adminId,
        AccountEnforcementAction? requestedAction,
        DateTime? suspendedUntil,
        CancellationToken cancellationToken);

    void Ban(User user, string reason);
    void Restore(User user);
    void SetActive(User user, bool isActive);

    void Suspend(User user, DateTime suspendedUntil, string? reason);

    void ClearSuspension(User user);

    Task<User?> ToggleActiveAsync(string email, CancellationToken cancellationToken);

    Task<User?> SuspendAsync(Guid userId, DateTime suspendedUntil, string? reason, CancellationToken cancellationToken);

    Task<User?> SuspendAsync(string email, DateTime suspendedUntil, string? reason, CancellationToken cancellationToken);

    Task<User?> ClearSuspensionAsync(string email, CancellationToken cancellationToken);
}

public sealed record AccountViolationSource(
    UserViolationSourceType SourceType,
    Guid? DisputeId = null,
    Guid? ReportId = null,
    Guid? ManualActionId = null,
    Guid? ContractId = null,
    Guid? MilestoneId = null);

public sealed record AccountEnforcementResult(
    bool Duplicate,
    int PreviousViolationCount,
    int ViolationCount,
    int PreviousAccountStatus,
    int AccountStatus,
    UserViolationAction? Action,
    DateTime? SuspendedUntil,
    Guid? UserViolationId);
