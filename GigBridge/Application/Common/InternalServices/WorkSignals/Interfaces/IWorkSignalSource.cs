namespace Application.Common.InternalServices.WorkSignals.Interfaces;

/// <summary>
/// What a background worker's idle loop awaits instead of a fixed <c>Task.Delay</c>. Returns
/// when either a signal arrives (new/re-armed work, published via Postgres NOTIFY and relayed
/// locally by <c>PostgresWorkSignalListener</c>) or <paramref name="deadlineUtc"/> passes,
/// whichever comes first — so a future-dated row still wakes the worker on time even if nothing
/// signals in the meantime.
/// </summary>
public interface IWorkSignalSource
{
    /// <summary>
    /// Waits for a signal or until <paramref name="deadlineUtc"/>, whichever is sooner. Pass
    /// <see langword="null"/> to wait for a signal only (no deadline).
    /// </summary>
    Task WaitAsync(DateTime? deadlineUtc, CancellationToken cancellationToken);
}
