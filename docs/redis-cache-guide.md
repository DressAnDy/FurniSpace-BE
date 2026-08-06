# FurniSpace Redis Cache Guide

This guide explains how Redis should be used in the FurniSpace backend. The repository already defines a Redis service in `docker-compose.yml`, but the application cache abstraction and implementation are still empty.

## 1. Current Status

Already available:

- Docker Compose service: `redis`
- Docker image: `redis:7-alpine`
- Password is read from `.env` through `REDIS_PASSWORD`
- Redis max memory is configured as `256mb`
- Eviction policy is `noeviction` to protect auth/session security keys from being evicted under memory pressure
- Redis health check runs `redis-cli ping`
- Application placeholder: `ICacheService`
- Infrastructure placeholder: `RedisCacheService`

Still missing:

- Redis connection string/configuration in the API app settings or environment variables.
- `StackExchange.Redis` package.
- `ICacheService` methods.
- `RedisCacheService` implementation.
- Dependency injection registration.
- Cache key conventions.
- Cache invalidation rules.

## 2. Redis in Docker Compose

Current Redis service:

```yaml
redis:
  image: redis:7-alpine
  container_name: furnispace-redis
  env_file: .env
  command: ["redis-server", "--requirepass", "${REDIS_PASSWORD}", "--maxmemory", "256mb", "--maxmemory-policy", "noeviction"]
  volumes:
    - redis_data:/data
  healthcheck:
    test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
  networks:
    - furnispace-network
```

Inside Docker Compose, the API should connect to Redis using host `redis` and port `6379`.

Example connection string inside Docker:

```text
redis:6379,password=${REDIS_PASSWORD},abortConnect=false
```

If developers need to connect from the host machine using Redis CLI or a GUI tool, expose the Redis port:

```yaml
ports:
  - "6379:6379"
```

Only expose this port for local development. Avoid exposing Redis directly in production.

## 3. Environment Variables

Recommended `.env` variables:

```env
REDIS_PASSWORD=change-me
REDIS_CONNECTION=redis:6379,password=change-me,abortConnect=false
```

Alternative appsettings structure:

```json
{
  "Redis": {
    "ConnectionString": "redis:6379,password=change-me,abortConnect=false",
    "InstanceName": "FurniSpace"
  }
}
```

For Docker Compose, environment variables are usually simpler because `.env` is already loaded by the container.

## 4. Required NuGet Package

Install Redis client package in Infrastructure:

```powershell
dotnet add src/FurniSpace.Infrastructure package StackExchange.Redis
```

The Application project should not reference Redis directly. Application only depends on `ICacheService`.

## 5. Recommended Interface

File:

```text
src/FurniSpace.Infrastructure/Interfaces/ICacheService.cs
```

Recommended contract:

```csharp
namespace FurniSpace.Infrastructure.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
```

Use generic methods so handlers can work with DTOs directly.

## 6. Redis Implementation

File:

```text
src/FurniSpace.Infrastructure/Caching/RedisCacheService.cs
```

Recommended implementation:

```csharp
using System.Text.Json;
using FurniSpace.Infrastructure.Interfaces;
using StackExchange.Redis;

namespace FurniSpace.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connection;

    public RedisCacheService(IConnectionMultiplexer connection)
    {
        _connection = connection;
        _database = connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(key);
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

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return _database.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            var keys = server.Keys(pattern: $"{prefix}*");

            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _database.KeyExistsAsync(key);
    }
}
```

Notes:

- `CancellationToken` is included for interface consistency, but `StackExchange.Redis` string operations do not accept it directly.
- `RemoveByPrefixAsync` uses key scanning and should be used carefully. Prefer deleting exact keys when possible.
- Avoid caching EF entities directly. Cache DTOs or read models.

## 7. Dependency Injection

File:

```text
src/FurniSpace.Infrastructure/DependencyInjection.cs
```

Recommended registration:

```csharp
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FurniSpace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration["REDIS_CONNECTION"]
            ?? configuration.GetSection("Redis")["ConnectionString"];

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));

            services.AddScoped<ICacheService, RedisCacheService>();
        }

        return services;
    }
}
```

If Redis is required for the application to boot, throw an exception when the connection string is missing. If Redis is optional, keep the guarded registration above and make features tolerate cache misses.

## 8. Program.cs Wiring

File:

```text
src/FurniSpace.API/Program.cs
```

When Infrastructure DI is implemented:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Because the repo already calls:

```csharp
EnvLoader.LoadEnv(required: false);
```

environment variables from `.env` can be used during local development.

## 9. Cache Key Convention

Use predictable, namespaced keys:

```text
furnispace:{module}:{resource}:{id}
furnispace:{module}:list:{hash}
```

Examples:

```text
furnispace:user:6fa459ea-ee8a-3ca4-894e-db77e160355e
furnispace:users:list:page-1:size-20
furnispace:furniture:catalog:page-1:size-24
furnispace:project:design:3d-scene:{projectId}
```

Rules:

- Use lowercase module names.
- Include tenant/user scope if the data is user-specific.
- Never cache private user data under a shared key.
- Keep list keys separate from item keys.
- Use prefixes that can be invalidated safely.

## 10. Recommended TTL

Suggested defaults:

| Data | TTL |
| --- | --- |
| User profile summary | 5-15 minutes |
| Furniture catalog item | 30-60 minutes |
| Furniture catalog list | 5-15 minutes |
| Project summary | 2-5 minutes |
| 3D design scene snapshot | 1-5 minutes |
| Auth/session data | Match token/session lifetime |
| Static reference data | 1-24 hours |

Short TTLs are safer while the domain is still changing.

## 11. Example Usage in a Handler

Example for `GetUserById`:

```csharp
public sealed class GetUserByIdHandler
{
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;

    public GetUserByIdHandler(IUserRepository users, ICacheService cache)
    {
        _users = users;
        _cache = cache;
    }

    public async Task<ServiceResult<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"furnispace:user:{request.Id}";
        var cached = await _cache.GetAsync<UserDto>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return ServiceResult<UserDto>.Success(cached);
        }

        var user = await _users.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserDto>.NotFound("User not found");
        }

        var dto = user.Adapt<UserDto>();
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return ServiceResult<UserDto>.Success(dto);
    }
}
```

## 12. Cache Invalidation

Whenever a write operation changes data, remove affected cache keys.

Examples:

| Action | Invalidate |
| --- | --- |
| Update user | `furnispace:user:{id}`, user list prefixes |
| Delete user | `furnispace:user:{id}`, user list prefixes |
| Update furniture item | `furnispace:furniture:{id}`, catalog list prefixes |
| Update project design | `furnispace:project:{id}`, `furnispace:project:design:3d-scene:{id}` |
| Create order | user order list prefixes, project/order summaries |

Prefer exact key deletion. Use prefix invalidation only when list cache keys cannot be enumerated easily.

## 13. What Should Be Cached

Good candidates:

- Read-heavy catalog data.
- Frequently requested user/profile summaries.
- Project summary views.
- 3D scene read models.
- Dashboard counters with short TTL.
- Static reference data.

Avoid caching:

- Raw password/token values.
- Highly sensitive user data.
- Data that changes very frequently.
- EF Core tracked entities.
- Large 3D assets if Redis memory is limited.

## 14. Redis for Authentication and Authorization

Redis should not be the primary source of truth for authentication or authorization. Use the database for users, password hashes, roles, permissions, and audit data. Use Redis as a fast temporary store for auth-related runtime state.

Recommended use cases:

| Use case | Recommended | Notes |
| --- | --- | --- |
| Refresh token/session storage | Yes | TTL should match refresh token lifetime |
| JWT blacklist/revocation | Yes | Store `jti` or token id until token expiry |
| Login rate limiting | Yes | Protect login endpoint from brute force |
| OTP/email verification code | Yes | Short TTL, usually 5-10 minutes |
| Password reset token | Yes | Short TTL, delete after successful use |
| Permission/role cache | Yes | Short TTL, invalidate when roles change |
| Password storage | No | Store only password hashes in the database |
| Permanent authorization decision | No | Database and ASP.NET Core policies remain the source of truth |

Recommended auth cache keys:

```text
furnispace:auth:refresh-token:{userId}:{tokenId}
furnispace:auth:blacklist:{jti}
furnispace:auth:login-attempt:{email}
furnispace:auth:otp:{email}
furnispace:auth:password-reset:{userId}:{tokenId}
furnispace:auth:permissions:{userId}
```

Recommended TTL:

| Auth data | TTL |
| --- | --- |
| Refresh token/session | Same as refresh token expiry |
| JWT blacklist item | Same as remaining JWT lifetime |
| Login attempt counter | 5-15 minutes |
| OTP/email verification | 5-10 minutes |
| Password reset token | 10-30 minutes |
| Permission/role cache | 2-10 minutes |

Example: blacklist a revoked JWT by `jti`:

```csharp
public async Task RevokeAccessTokenAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken)
{
    var ttl = expiresAt - DateTime.UtcNow;
    if (ttl <= TimeSpan.Zero)
    {
        return;
    }

    var cacheKey = $"furnispace:auth:blacklist:{jti}";
    await _cache.SetAsync(cacheKey, true, ttl, cancellationToken);
}
```

Example: check whether a JWT is blacklisted:

```csharp
public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken)
{
    var cacheKey = $"furnispace:auth:blacklist:{jti}";
    return _cache.ExistsAsync(cacheKey, cancellationToken);
}
```

Example: login rate limit counter:

```text
key: furnispace:auth:login-attempt:{email}
value: current failed attempt count
ttl: 10 minutes
```

The current `ICacheService` is enough for simple set/get/remove scenarios. For atomic counters, add Redis-specific methods later:

```csharp
Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
```

Authorization guidance:

- Use ASP.NET Core authorization policies and roles for request authorization.
- Cache permissions in Redis only to reduce database reads.
- Invalidate `furnispace:auth:permissions:{userId}` immediately when an admin changes a user role or permission.
- Never rely on a long-lived permission cache for critical authorization decisions.

Recommended FurniSpace auth design:

- Authentication: JWT access token + refresh token.
- Database: users, password hash, roles, permissions, refresh token metadata if audit is needed.
- Redis: active refresh/session state, JWT blacklist, OTP, password reset token, login rate limit, short-lived permission cache.
- Authorization: ASP.NET Core policies using role/permission data loaded from DB or short-lived Redis cache.

## 15. Local Testing

Start Redis through Docker Compose:

```powershell
docker compose up -d redis
```

Check Redis health inside the container:

```powershell
docker compose exec redis redis-cli -a $env:REDIS_PASSWORD ping
```

Expected response:

```text
PONG
```

Run the application tests after implementing Redis:

```powershell
dotnet test tests/UnitTests/FurniSpace.Application.Tests/FurniSpace.Application.Tests.csproj
dotnet test tests/UnitTests/FurniSpace.Infrastructure.Tests/FurniSpace.Infrastructure.Tests.csproj
```

## 16. Implementation Checklist

- [x] Add `StackExchange.Redis` to Infrastructure.
- [x] Add Redis connection string via `.env`.
- [x] Fill `ICacheService`.
- [x] Implement `RedisCacheService`.
- [x] Register `IConnectionMultiplexer`.
- [x] Register `ICacheService`.
- [x] Add cache key naming helper if keys become repeated.
- [ ] Add cache read path to selected query handlers.
- [ ] Add invalidation to related command handlers.
- [x] Add auth keys for refresh token, JWT blacklist, OTP, password reset, and permission cache if needed.
- [x] Add atomic counter support before implementing login rate limiting.
- [ ] Add invalidation when user roles or permissions change.
- [ ] Add tests for serialization, cache hit, cache miss, and invalidation.

Implemented auth base:

- [x] Add JWT settings loaded from `.env`.
- [x] Add JWT access token generation.
- [x] Add refresh token generation.
- [x] Store refresh tokens in Redis.
- [x] Revoke refresh tokens from Redis.
- [x] Store revoked access token `jti` values in Redis blacklist.
- [x] Check Redis blacklist during JWT bearer validation.
- [x] Add authorized logout endpoint that revokes refresh/access tokens.
