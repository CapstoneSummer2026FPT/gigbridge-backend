namespace Application.Common.InternalServices.Accounts.Models;

public sealed record AccountAccessState(
    bool Exists,
    bool IsActive,
    int AccountStatus,
    DateTime? SuspendedUntil);

public static class AccountAccessCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    public static string Key(Guid userId) => $"account-access:{userId:N}";
}
