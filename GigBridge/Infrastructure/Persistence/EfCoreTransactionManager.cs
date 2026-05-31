using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

public class EfCoreTransactionManager : ITransactionManager
{
    private readonly GigbridgeDbContext _dbContext;

    public EfCoreTransactionManager(GigbridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfCoreApplicationTransaction(transaction);
    }

    private sealed class EfCoreApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfCoreApplicationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.RollbackAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
