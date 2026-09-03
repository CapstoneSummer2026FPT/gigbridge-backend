using Application.Common.InternalServices.WorkSignals.Interfaces;

namespace Application.Common.InternalServices.WorkSignals.Services;

/// <summary>
/// In-process wake-up gate for one channel. <see cref="Signal"/> is called only by
/// <c>PostgresWorkSignalListener</c> when it relays a NOTIFY for this gate's channel;
/// <see cref="WaitAsync"/> is what workers consume via <see cref="IWorkSignalSource"/>.
/// One instance is registered per channel (keyed DI); <c>DeliveryOutboxService</c>'s realtime and
/// email loops both wait on the same instance for the delivery-outbox channel, so this has to
/// support more than one concurrent waiter, not just one.
/// </summary>
public sealed class WorkSignalGate : IWorkSignalSource
{
    private readonly object _lock = new();
    private TaskCompletionSource<bool> _tcs = New();
    private int _waiterCount;
    private bool _pending;

    private static TaskCompletionSource<bool> New() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Wakes every current waiter immediately. Called by the Postgres listener only.
    ///
    /// If nobody is waiting at the moment this fires — e.g. a worker just finished querying the
    /// database, found nothing, and a NOTIFY lands in the gap before it calls
    /// <see cref="WaitAsync"/> — the signal is remembered and consumed by the very next
    /// <see cref="WaitAsync"/> call instead of being silently dropped. Without this, that call
    /// would block for its full deadline (up to the max idle interval) even though the work it
    /// was about to look for had just arrived.
    /// </summary>
    public void Signal()
    {
        TaskCompletionSource<bool> toRelease;
        lock (_lock)
        {
            toRelease = _tcs;
            _tcs = New();
            if (_waiterCount == 0)
            {
                _pending = true;
            }
        }

        toRelease.TrySetResult(true);
    }

    public async Task WaitAsync(DateTime? deadlineUtc, CancellationToken cancellationToken)
    {
        Task signalTask;
        lock (_lock)
        {
            if (_pending)
            {
                _pending = false;
                return;
            }

            _waiterCount++;
            signalTask = _tcs.Task;
        }

        try
        {
            if (deadlineUtc is null)
            {
                await signalTask.WaitAsync(cancellationToken);
                return;
            }

            var delay = deadlineUtc.Value - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                await signalTask.WaitAsync(delay, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Deadline reached without a signal — a normal wake, not an error.
            }
        }
        finally
        {
            lock (_lock)
            {
                _waiterCount--;
            }
        }
    }
}
