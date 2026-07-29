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
        var dbTransaction = _transaction.GetDbTransaction();
        await using var command = dbTransaction.Connection!.CreateCommand();
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
