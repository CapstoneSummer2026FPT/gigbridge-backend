using Application.Common.InternalServices.WorkSignals.Services;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.WorkSignals.Services;

public sealed class WorkSignalGateTests
{
    [Fact]
    public async Task WaitAsync_ReturnsImmediately_WhenDeadlineIsInThePast()
    {
        var gate = new WorkSignalGate();

        var task = gate.WaitAsync(DateTime.UtcNow.AddSeconds(-1), CancellationToken.None);

        Assert.True(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1))) == task);
    }

    [Fact]
    public async Task WaitAsync_CompletesOnSignal_BeforeItsDeadline()
    {
        var gate = new WorkSignalGate();
        var waitTask = gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), CancellationToken.None);

        // Give the waiter a moment to actually start waiting before signaling.
        await Task.Delay(20);
        gate.Signal();

        var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(waitTask, completed);
    }

    [Fact]
    public async Task WaitAsync_ReturnsAtDeadline_WhenNeverSignaled()
    {
        var gate = new WorkSignalGate();
        var deadline = DateTime.UtcNow.AddMilliseconds(100);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await gate.WaitAsync(deadline, CancellationToken.None);
        sw.Stop();

        Assert.InRange(sw.ElapsedMilliseconds, 0, 5_000);
    }

    [Fact]
    public async Task WaitAsync_ThrowsOperationCanceled_WhenCancelledBeforeSignalOrDeadline()
    {
        var gate = new WorkSignalGate();
        using var cts = new CancellationTokenSource();
        var waitTask = gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), cts.Token);

        await Task.Delay(20);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task WaitAsync_WithNullDeadline_WaitsUntilSignaled()
    {
        var gate = new WorkSignalGate();
        var waitTask = gate.WaitAsync(null, CancellationToken.None);

        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        gate.Signal();

        var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(waitTask, completed);
    }

    [Fact]
    public async Task Signal_WakesMultipleConcurrentWaiters()
    {
        var gate = new WorkSignalGate();
        var first = gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), CancellationToken.None);
        var second = gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), CancellationToken.None);

        await Task.Delay(20);
        gate.Signal();

        var completed = await Task.WhenAny(Task.WhenAll(first, second), Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Signal_BeforeWaitAsync_WakesTheNextWaitImmediately()
    {
        // The lost-wakeup case: a signal fires while nobody is currently waiting (e.g. a worker
        // just finished querying, found nothing, and a NOTIFY lands before it calls WaitAsync).
        // That signal must be honored by the very next WaitAsync call, not silently dropped —
        // otherwise the worker would block for its full deadline despite the work it was about
        // to look for having just arrived.
        var gate = new WorkSignalGate();
        gate.Signal();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await gate.WaitAsync(deadline, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1_000, $"Expected the pending signal to wake the wait immediately, but it took {sw.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task Signal_BeforeWaitAsync_OnlyCoalescesToOnePendingWake()
    {
        // A pending signal is consumed exactly once — a second WaitAsync call right after must
        // genuinely wait again, not also return instantly off the same signal.
        var gate = new WorkSignalGate();
        gate.Signal();

        await gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), CancellationToken.None);

        var deadline = DateTime.UtcNow.AddMilliseconds(150);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await gate.WaitAsync(deadline, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 100, $"Expected the second wait to reach its deadline, but returned after {sw.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task Signal_WhileWaitersArePresent_DoesNotAlsoLeaveAPendingWakeForTheNextCall()
    {
        // When Signal() fires while waiters exist, it broadcasts to them directly — it must not
        // also set a pending flag that would make some later, unrelated WaitAsync call return
        // instantly.
        var gate = new WorkSignalGate();
        var waiter = gate.WaitAsync(DateTime.UtcNow.AddSeconds(30), CancellationToken.None);

        await Task.Delay(20);
        gate.Signal();
        await waiter;

        var deadline = DateTime.UtcNow.AddMilliseconds(150);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await gate.WaitAsync(deadline, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 100, $"Expected this later wait to reach its deadline, but returned after {sw.ElapsedMilliseconds}ms.");
    }
}
