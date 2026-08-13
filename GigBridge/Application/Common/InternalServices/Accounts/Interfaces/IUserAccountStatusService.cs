using Domain.Entities;
using Domain.Enums.Accounts;
using Application.Common.InternalServices.Accounts.Models;

namespace Application.Common.InternalServices.Accounts.Interfaces;

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
