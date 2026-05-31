namespace Application.Common.Interfaces;

public interface ITransactionManager
{
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
