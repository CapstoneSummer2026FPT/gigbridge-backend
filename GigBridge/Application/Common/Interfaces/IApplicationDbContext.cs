using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<IApplicationDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IApplicationDbContextTransaction : IAsyncDisposable
{
    Task AcquireTransactionLockAsync(long lockKey, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}
