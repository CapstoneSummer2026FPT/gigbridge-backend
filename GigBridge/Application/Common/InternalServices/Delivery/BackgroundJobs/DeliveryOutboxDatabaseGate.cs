namespace Application.Common.InternalServices.Delivery.BackgroundJobs;

internal sealed class DeliveryOutboxDatabaseGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public DeliveryOutboxDatabaseGate(int maximumConcurrency)
    {
        _semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
