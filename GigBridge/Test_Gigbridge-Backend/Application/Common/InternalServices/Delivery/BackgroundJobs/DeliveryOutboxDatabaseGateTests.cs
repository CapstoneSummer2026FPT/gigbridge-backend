using Application.Common.InternalServices.Delivery.BackgroundJobs;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Delivery.BackgroundJobs;

public sealed class DeliveryOutboxDatabaseGateTests
{
    [Fact]
    public async Task RunAsync_SerializesOperationsAtConfiguredLimit()
    {
        using var gate = new DeliveryOutboxDatabaseGate(1);
        var firstEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.RunAsync(async () =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task;
        }, CancellationToken.None);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var second = gate.RunAsync(() =>
        {
            secondEntered.SetResult();
            return Task.CompletedTask;
        }, CancellationToken.None);

        var completedBeforeRelease = await Task.WhenAny(
            secondEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert.NotSame(secondEntered.Task, completedBeforeRelease);

        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(secondEntered.Task.IsCompletedSuccessfully);
    }
}
