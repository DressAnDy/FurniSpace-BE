# Huong Dan Redis Cache Cho FurniSpace

Tai lieu nay huong dan cach su dung Redis trong backend FurniSpace. Repo hien da khai bao Redis trong `docker-compose.yml`, nhung abstraction va implementation cache trong code van dang la placeholder.

## 1. Hien Trang

Da co san:

- Service Docker Compose: `redis`
- Docker image: `redis:7-alpine`
- Password doc tu `.env` thong qua `REDIS_PASSWORD`
- Redis max memory dang la `256mb`
- Chinh sach eviction dang la `allkeys-lru`
- Redis health check chay `redis-cli ping`
- Placeholder Application: `ICacheService`
- Placeholder Infrastructure: `RedisCacheService`

Con thieu:

- Connection string/configuration Redis trong app settings hoac environment variables.
- Package `StackExchange.Redis`.
- Cac method trong `ICacheService`.
- Implementation `RedisCacheService`.
- Dependency injection registration.
- Quy uoc dat cache key.
- Quy tac invalidate cache.

## 2. Redis Trong Docker Compose

Service Redis hien tai:

```yaml
redis:
  image: redis:7-alpine
  container_name: furnispace-redis
  env_file: .env
  command: ["redis-server", "--requirepass", "${REDIS_PASSWORD}", "--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru"]
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

Khi API chay trong Docker Compose, API nen ket noi Redis bang host `redis` va port `6379`.

Connection string mau trong Docker:

```text
redis:6379,password=${REDIS_PASSWORD},abortConnect=false
```

Neu dev muon ket noi Redis tu may host bang Redis CLI hoac GUI tool, co the expose port:

```yaml
ports:
  - "6379:6379"
```

Chi nen expose port nay cho local development. Khong expose Redis truc tiep o production.

## 3. Environment Variables

Bien `.env` de xuat:

```env
REDIS_PASSWORD=change-me
REDIS_CONNECTION=redis:6379,password=change-me,abortConnect=false
```

Hoac cau truc appsettings:

```json
{
  "Redis": {
    "ConnectionString": "redis:6379,password=change-me,abortConnect=false",
    "InstanceName": "FurniSpace"
  }
}
```

Voi Docker Compose, environment variables thuong don gian hon vi container da doc `.env`.

## 4. Package Can Cai

Cai Redis client package vao Infrastructure:

```powershell
dotnet add src/FurniSpace.Infrastructure package StackExchange.Redis
```

Application project khong nen reference Redis truc tiep. Application chi phu thuoc `ICacheService`.

## 5. Interface De Xuat

File:

```text
src/FurniSpace.Application/Interfaces/ICacheService.cs
```

Contract de xuat:

```csharp
namespace FurniSpace.Application.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
```

Dung generic methods de handler co the lam viec truc tiep voi DTO.

## 6. Redis Implementation

File:

```text
src/FurniSpace.Infrastructure/Caching/RedisCacheService.cs
```

Implementation de xuat:

```csharp
using System.Text.Json;
using FurniSpace.Application.Interfaces;
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

Luu y:

- `CancellationToken` duoc giu trong interface de dong nhat contract, nhung mot so operation cua `StackExchange.Redis` khong nhan token truc tiep.
- `RemoveByPrefixAsync` scan key, nen can dung can than. Uu tien xoa exact key neu co the.
- Khong cache EF entity truc tiep. Nen cache DTO hoac read model.

## 7. Dependency Injection

File:

```text
src/FurniSpace.Infrastructure/DependencyInjection.cs
```

Registration de xuat:

```csharp
using FurniSpace.Application.Interfaces;
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

Neu Redis la dependency bat buoc, nen throw exception khi thieu connection string. Neu Redis la optional, giu guard nhu tren va dam bao feature van hoat dong khi cache miss.

## 8. Noi Vao Program.cs

File:

```text
src/FurniSpace.API/Program.cs
```

Khi Infrastructure DI da implement:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Repo hien da co:

```csharp
EnvLoader.LoadEnv(required: false);
```

Nen local development co the doc environment variables tu `.env`.

## 9. Quy Uoc Cache Key

Dung key co namespace ro rang:

```text
furnispace:{module}:{resource}:{id}
furnispace:{module}:list:{hash}
```

Vi du:

```text
furnispace:user:6fa459ea-ee8a-3ca4-894e-db77e160355e
furnispace:users:list:page-1:size-20
furnispace:furniture:catalog:page-1:size-24
furnispace:project:design:3d-scene:{projectId}
```

Quy tac:

- Dung ten module lowercase.
- Them scope tenant/user neu data la cua rieng tung user.
- Khong cache private user data bang shared key.
- Tach key list va key item.
- Dung prefix co the invalidate an toan.

## 10. TTL De Xuat

Goi y TTL:

| Data | TTL |
| --- | --- |
| User profile summary | 5-15 phut |
| Furniture catalog item | 30-60 phut |
| Furniture catalog list | 5-15 phut |
| Project summary | 2-5 phut |
| 3D design scene snapshot | 1-5 phut |
| Auth/session data | Theo token/session lifetime |
| Static reference data | 1-24 gio |

Khi domain con thay doi nhieu, nen dung TTL ngan de an toan.

## 11. Vi Du Dung Trong Handler

Vi du cho `GetUserById`:

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

Khi write operation thay doi data, can xoa cac cache key lien quan.

Vi du:

| Action | Invalidate |
| --- | --- |
| Update user | `furnispace:user:{id}`, user list prefixes |
| Delete user | `furnispace:user:{id}`, user list prefixes |
| Update furniture item | `furnispace:furniture:{id}`, catalog list prefixes |
| Update project design | `furnispace:project:{id}`, `furnispace:project:design:3d-scene:{id}` |
| Create order | user order list prefixes, project/order summaries |

Uu tien xoa exact key. Chi dung prefix invalidation khi khong the liet ke list cache key de xoa truc tiep.

## 13. Nen Cache Gi

Nen cache:

- Catalog data doc nhieu.
- User/profile summary duoc request thuong xuyen.
- Project summary views.
- 3D scene read models.
- Dashboard counters voi TTL ngan.
- Static reference data.

Khong nen cache:

- Raw password/token values.
- Du lieu user qua nhay cam.
- Data thay doi rat thuong xuyen.
- EF Core tracked entities.
- 3D assets qua lon khi Redis memory bi gioi han.

## 14. Redis Cho Authentication Va Authorization

Redis khong nen la source of truth chinh cho authentication hoac authorization. Database van nen luu user, password hash, role, permission va audit data. Redis nen duoc dung nhu mot temporary store nhanh cho runtime state lien quan den auth.

Use case de xuat:

| Use case | Nen dung | Ghi chu |
| --- | --- | --- |
| Luu refresh token/session | Co | TTL nen bang refresh token lifetime |
| JWT blacklist/revocation | Co | Luu `jti` hoac token id den khi token het han |
| Rate limit login | Co | Bao ve login endpoint khoi brute force |
| OTP/email verification code | Co | TTL ngan, thuong 5-10 phut |
| Password reset token | Co | TTL ngan, xoa sau khi dung thanh cong |
| Cache permission/role | Co | TTL ngan, invalidate khi role thay doi |
| Luu password | Khong | Chi luu password hash trong database |
| Quyet dinh authorization lau dai | Khong | Database va ASP.NET Core policies van la source of truth |

Auth cache keys de xuat:

```text
furnispace:auth:refresh-token:{userId}:{tokenId}
furnispace:auth:blacklist:{jti}
furnispace:auth:login-attempt:{email}
furnispace:auth:otp:{email}
furnispace:auth:password-reset:{userId}:{tokenId}
furnispace:auth:permissions:{userId}
```

TTL de xuat:

| Auth data | TTL |
| --- | --- |
| Refresh token/session | Bang refresh token expiry |
| JWT blacklist item | Bang thoi gian con lai cua JWT |
| Login attempt counter | 5-15 phut |
| OTP/email verification | 5-10 phut |
| Password reset token | 10-30 phut |
| Permission/role cache | 2-10 phut |

Vi du: blacklist JWT da bi revoke bang `jti`:

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

Vi du: kiem tra JWT co nam trong blacklist khong:

```csharp
public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken)
{
    var cacheKey = $"furnispace:auth:blacklist:{jti}";
    return _cache.ExistsAsync(cacheKey, cancellationToken);
}
```

Vi du: login rate limit counter:

```text
key: furnispace:auth:login-attempt:{email}
value: current failed attempt count
ttl: 10 minutes
```

`ICacheService` hien tai du cho cac case set/get/remove don gian. Neu can atomic counter, co the them method Redis-specific sau:

```csharp
Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
```

Huong dan authorization:

- Dung ASP.NET Core authorization policies va roles de authorize request.
- Chi cache permissions trong Redis de giam database reads.
- Invalidate `furnispace:auth:permissions:{userId}` ngay khi admin thay doi role hoac permission cua user.
- Khong dua vao permission cache song qua lau cho cac authorization decision quan trong.

Auth design de xuat cho FurniSpace:

- Authentication: JWT access token + refresh token.
- Database: users, password hash, roles, permissions, refresh token metadata neu can audit.
- Redis: active refresh/session state, JWT blacklist, OTP, password reset token, login rate limit, permission cache ngan han.
- Authorization: ASP.NET Core policies dung role/permission data tu DB hoac Redis cache ngan han.

## 15. Test Local

Start Redis bang Docker Compose:

```powershell
docker compose up -d redis
```

Kiem tra health Redis trong container:

```powershell
docker compose exec redis redis-cli -a $env:REDIS_PASSWORD ping
```

Ket qua mong doi:

```text
PONG
```

Chay test sau khi implement Redis:

```powershell
dotnet test tests/FurniSpace.Application.Tests/FurniSpace.Application.Tests.csproj
dotnet test tests/FurniSpace.Infrastructure.Tests/FurniSpace.Infrastructure.Tests.csproj
```

## 16. Checklist Implement

- [ ] Them `StackExchange.Redis` vao Infrastructure.
- [ ] Them Redis connection string qua `.env` hoac `appsettings`.
- [ ] Fill `ICacheService`.
- [ ] Implement `RedisCacheService`.
- [ ] Register `IConnectionMultiplexer`.
- [ ] Register `ICacheService`.
- [ ] Them cache key naming helper neu key bi lap lai nhieu.
- [ ] Them cache read path vao query handler phu hop.
- [ ] Them invalidation vao command handler lien quan.
- [ ] Them auth keys cho refresh token, JWT blacklist, OTP, password reset va permission cache neu can.
- [ ] Them atomic counter support truoc khi implement login rate limiting.
- [ ] Them invalidation khi user role hoac permission thay doi.
- [ ] Them test cho serialization, cache hit, cache miss va invalidation.
