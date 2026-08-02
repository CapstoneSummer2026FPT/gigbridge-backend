using Application.Common.Interfaces;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

internal sealed class EfApplicationDbContextTransaction : IApplicationDbContextTransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfApplicationDbContextTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task AcquireTransactionLockAsync(
        long lockKey,
        CancellationToken cancellationToken)
    {
        // Providers without a real SQL transaction (e.g. the EF Core in-memory store
        // used by tests) expose no DbTransaction, so the Postgres advisory lock is a
        // no-op there. Production always runs on Npgsql and takes the lock.
        DbTransaction? dbTransaction;
        try
        {
            dbTransaction = _transaction.GetDbTransaction();
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            // EF Core throws these when the provider is not relational (in-memory store).
            dbTransaction = null;
        }

        if (dbTransaction?.Connection is null)
        {
            return;
        }

        await using var command = dbTransaction.Connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(@lockKey);";

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "lockKey";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        return _transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _transaction.DisposeAsync();
    }
}
