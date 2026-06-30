using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IUserAccountStatusService
{
    void SetActive(User user, bool isActive);

    void Suspend(User user, DateTime suspendedUntil, string? reason);

    void ClearSuspension(User user);

    Task<User?> ToggleActiveAsync(string email, CancellationToken cancellationToken);

    Task<User?> SuspendAsync(Guid userId, DateTime suspendedUntil, string? reason, CancellationToken cancellationToken);

    Task<User?> SuspendAsync(string email, DateTime suspendedUntil, string? reason, CancellationToken cancellationToken);

    Task<User?> ClearSuspensionAsync(string email, CancellationToken cancellationToken);
}
