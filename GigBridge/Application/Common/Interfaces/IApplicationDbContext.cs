using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    bool SupportsRelationalBulkOperations { get; }
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<IApplicationDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    void ResetChangeTracker();
}

public interface IApplicationDbContextTransaction : IAsyncDisposable
{
    Task AcquireTransactionLockAsync(
        long lockKey,
        CancellationToken cancellationToken,
        string? lockPurpose = null,
        [CallerFilePath] string callerFilePath = "");
    Task CommitAsync(CancellationToken cancellationToken);
}
