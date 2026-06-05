using System.Text.Json;
using FurniSpace.Infrastructure.Interfaces;
using StackExchange.Redis;

namespace FurniSpace.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer connection)
    {
        _connection = connection;
        _database = connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(key);
        return Deserialize<T>(value);
    }

    public async Task<T?> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetDeleteAsync(key);
        return Deserialize<T>(value);
    }

    public async Task<bool> CompareAndRemoveAsync<T>(
        string key,
        T expectedValue,
        CancellationToken cancellationToken = default)
    {
        const string script = """
            if redis.call("GET", KEYS[1]) == ARGV[1] then
                return redis.call("DEL", KEYS[1])
            end
            return 0
            """;
        var serialized = JsonSerializer.Serialize(expectedValue, JsonOptions);
        var result = await _database.ScriptEvaluateAsync(
            script,
            [new RedisKey(key)],
            [new RedisValue(serialized)]);
        return (int)result == 1;
    }

    private static T? Deserialize<T>(RedisValue value)
    {
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value!, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        await _database.StringSetAsync(key, serialized, expiration);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);

            await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
            {
                await _database.KeyDeleteAsync(key);
            }
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _database.KeyExistsAsync(key);
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringIncrementAsync(key);

        if (expiration.HasValue && value == 1)
        {
            await _database.KeyExpireAsync(key, expiration);
        }

        return value;
    }
}
