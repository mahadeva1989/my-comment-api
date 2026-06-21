using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

public class CacheService(IDistributedCache cache)
{
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var data = await cache.GetStringAsync(key, ct);
        return data is null ? default : JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var data = JsonSerializer.Serialize(value);
        await cache.SetStringAsync(key, data, DefaultOptions, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await cache.RemoveAsync(key, ct);
    }
}