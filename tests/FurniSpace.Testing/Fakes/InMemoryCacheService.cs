using System.Collections.Concurrent;
using System.Text.Json;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Testing.Fakes;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _values = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetValue<T>(key, remove: false));

    public Task<T?> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetValue<T>(key, remove: true));

    public Task<bool> CompareAndRemoveAsync<T>(
        string key,
        T expectedValue,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetEntry(key, out var entry)
            || entry.SerializedValue != JsonSerializer.Serialize(expectedValue))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_values.TryRemove(new KeyValuePair<string, CacheEntry>(key, entry)));
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        _values[key] = new CacheEntry(
            JsonSerializer.Serialize(value),
            expiration.HasValue ? DateTimeOffset.UtcNow.Add(expiration.Value) : null);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _values.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetEntry(key, out _));

    public Task<long> IncrementAsync(
        string key,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTimeOffset.UtcNow;
            if (!TryGetEntry(key, out var existing))
            {
                var created = new CacheEntry(
                    JsonSerializer.Serialize(1L),
                    expiration.HasValue ? now.Add(expiration.Value) : null);
                if (_values.TryAdd(key, created))
                {
                    return Task.FromResult(1L);
                }

                continue;
            }

            var current = JsonSerializer.Deserialize<long>(existing.SerializedValue);
            var updated = existing with { SerializedValue = JsonSerializer.Serialize(current + 1) };
            if (_values.TryUpdate(key, updated, existing))
            {
                return Task.FromResult(current + 1);
            }
        }
    }

    private T? TryGetValue<T>(string key, bool remove)
    {
        if (!TryGetEntry(key, out var entry))
        {
            return default;
        }

        if (remove)
        {
            _values.TryRemove(new KeyValuePair<string, CacheEntry>(key, entry));
        }

        return JsonSerializer.Deserialize<T>(entry.SerializedValue);
    }

    private bool TryGetEntry(string key, out CacheEntry entry)
    {
        if (!_values.TryGetValue(key, out entry!))
        {
            return false;
        }

        if (entry.ExpiresAt is null || entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _values.TryRemove(new KeyValuePair<string, CacheEntry>(key, entry));
        entry = null!;
        return false;
    }

    private sealed record CacheEntry(string SerializedValue, DateTimeOffset? ExpiresAt);
}
