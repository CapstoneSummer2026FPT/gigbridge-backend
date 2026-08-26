using Application.Common.Interfaces;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Persistence;

internal sealed class EfApplicationDbContextTransaction : IApplicationDbContextTransaction
{
    private static readonly Meter Meter = new("GigBridge.Database");
    private static readonly Histogram<double> LockWaitMilliseconds = Meter.CreateHistogram<double>(
        "gigbridge.database.advisory_lock.wait", "ms", "PostgreSQL transaction advisory lock wait time.");
    private readonly IDbContextTransaction _transaction;

    public EfApplicationDbContextTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task AcquireTransactionLockAsync(
        long lockKey,
        CancellationToken cancellationToken,
        string? lockPurpose = null,
        string callerFilePath = "")
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

        var purpose = string.IsNullOrWhiteSpace(lockPurpose)
            ? Path.GetFileNameWithoutExtension(callerFilePath)
            : lockPurpose;
        var started = Stopwatch.GetTimestamp();
        await command.ExecuteNonQueryAsync(cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);
        LockWaitMilliseconds.Record(elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("lock.purpose", purpose));
        if (elapsed >= TimeSpan.FromSeconds(2))
        {
            Trace.TraceWarning(
                "PostgreSQL advisory lock {0} waited {1:F0}ms (key={2}).",
                purpose, elapsed.TotalMilliseconds, lockKey);
        }
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
