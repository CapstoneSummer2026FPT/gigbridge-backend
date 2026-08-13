namespace Application.Common.Interfaces.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    async Task<T?> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync<T>(key, cancellationToken);
        if (value is not null)
        {
            await RemoveAsync(key, cancellationToken);
        }

        return value;
    }
}
