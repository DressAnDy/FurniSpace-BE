#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Tests.TestDoubles;

internal sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, string> _values = new();

    public IReadOnlyCollection<string> SerializedValues => _values.Values;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_values.TryGetValue(key, out var value)
            ? JsonSerializer.Deserialize<T>(value)
            : default);
    }

    public Task<T?> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = _values.TryGetValue(key, out var existing)
            ? JsonSerializer.Deserialize<T>(existing)
            : default;
        _values.Remove(key);
        return Task.FromResult(value);
    }

    public Task<bool> CompareAndRemoveAsync<T>(
        string key,
        T expectedValue,
        CancellationToken cancellationToken = default)
    {
        var expected = JsonSerializer.Serialize(expectedValue);
        if (!_values.TryGetValue(key, out var actual) || actual != expected)
        {
            return Task.FromResult(false);
        }

        _values.Remove(key);
        return Task.FromResult(true);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        _values[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _values.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_values.ContainsKey(key));
    }

    public Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var current = _values.TryGetValue(key, out var value) && long.TryParse(value, out var number)
            ? number
            : 0;
        current++;
        _values[key] = current.ToString();
        return Task.FromResult(current);
    }
}
