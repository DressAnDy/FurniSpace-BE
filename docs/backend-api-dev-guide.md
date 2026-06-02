# FurniSpace Backend Developer Guide

This guide is the backend implementation standard for FurniSpace. It is written for human developers and coding agents. Use it as the source of truth when adding APIs, handlers, domain logic, persistence, caching, or authentication-related code.

The project follows a DDD-oriented layered architecture:

```text
API / Presentation
  -> Application
    -> Infrastructure
      -> Domain
```

`Infrastructure` owns persistence and external-provider details. `Application` owns service orchestration and may depend on repository/provider contracts exposed by `Infrastructure`, but business rules should not live in Infrastructure.

## 1. Project Map

| Project | Layer | Responsibility |
| --- | --- | --- |
| `FurniSpace.API` | Presentation | HTTP routing, controllers, middleware, auth pipeline, response mapping |
| `FurniSpace.Application` | Application | Use cases, DTOs, result contracts, validation, application services |
| `FurniSpace.Domain` | Domain | Entities, value objects, domain events, specifications, domain exceptions |
| `FurniSpace.Infrastructure` | Infrastructure | EF Core, PostgreSQL, Redis, JWT, repositories, external services |
| `FurniSpace.Shared` | Shared utilities | Cross-cutting helpers that do not belong to a business layer |

Dependency direction:

```text
API -> Application
API -> Shared
Application -> Infrastructure
Application -> Domain
Infrastructure -> Domain
Shared -> independent
```

Rules:

- `Domain` must not reference `Application`, `Infrastructure`, or `API`.
- `Application` services may use repository/provider interfaces from `Infrastructure`, but must not call EF Core, Redis, JWT libraries, HTTP clients, or external SDK APIs directly.
- Repository contracts and implementations live in `Infrastructure`.
- `API` should stay thin. It should not contain business rules.
- Cross-layer data should move through DTOs, commands, queries, results, and interfaces.

## 2. Request Flow

Normal HTTP flow:

```text
HTTP Request
  -> Controller
  -> Command/Query or Application service
  -> Handler / Use case
  -> Domain entity/value object
  -> Infrastructure repository/provider contract
  -> Infrastructure implementation
  -> Database/Redis/external service
  -> DTO
  -> ServiceResult<T>
  -> HTTP Response
```

Where logic belongs:

| Logic | Layer |
| --- | --- |
| Route, request binding, HTTP status response | API |
| Use case orchestration | Application |
| Validation of request shape | Application |
| Core business invariants | Domain |
| Entity state changes | Domain |
| Database query/write details | Infrastructure |
| Redis/cache/JWT/email/external APIs | Infrastructure |
| Mapping Domain to DTO | Application |

## 3. Layer Responsibilities

### API Layer

Use `FurniSpace.API` for:

- Controllers.
- Middleware.
- Authentication/authorization pipeline.
- HTTP status conversion.
- Swagger/OpenAPI.
- Calling Application handlers/services.

Do not put these in API:

- Business decisions.
- EF Core queries.
- Redis operations.
- Password hashing implementation.
- JWT token creation logic.

Controller shape:

```csharp
public sealed class ProductsController : BaseApiController
{
    private readonly ISender _sender;

    public ProductsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }
}
```

### Application Layer

Use `FurniSpace.Application` for:

- Commands and queries.
- Handlers/use cases.
- DTOs.
- Validation.
- Result contracts.
- Application service interfaces.
- Mapping configuration.

Application coordinates work through Infrastructure contracts, but it should not contain EF Core queries, Redis commands, JWT creation, or SMTP implementation details.

Recommended feature folder:

```text
FurniSpace.Application/
  Features/
    Products/
      Commands/
        CreateProduct/
          CreateProductCommand.cs
          CreateProductHandler.cs
          CreateProductValidator.cs
      Queries/
        GetProductById/
          GetProductByIdQuery.cs
          GetProductByIdHandler.cs
```

### Domain Layer

Use `FurniSpace.Domain` for:

- Entities.
- Aggregate roots.
- Value objects.
- Domain events.
- Specifications.
- Domain exceptions.

Domain objects should protect their own invariants:

```csharp
public sealed class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public Money Price { get; private set; } = default!;

    private Product() { }

    public static Product Create(string name, Money price)
    {
        return new Product
        {
            Name = name.Trim(),
            Price = price
        };
    }

    public void ChangePrice(Money price)
    {
        Price = price;
        SetUpdatedAt();
    }
}
```

Prefer value objects for fields with rules, such as `Email`, `Money`, and `Address`.

### Infrastructure Layer

Use `FurniSpace.Infrastructure` for:

- EF Core `DbContext`.
- Entity configurations.
- Repository implementations.
- Unit of Work implementation.
- Redis cache implementation.
- JWT token service.
- Refresh token store.
- Repository/provider interfaces.
- External provider implementations.
- Email/storage/external provider implementations.

Repository contracts and implementations stay in Infrastructure.

Example:

```text
Infrastructure/Interfaces/ICacheService.cs
Infrastructure/Caching/RedisCacheService.cs
```

## 4. Standard API Implementation Workflow

Follow this order when adding a new feature or endpoint.

### Step 1: Define the API Contract

Write the endpoint contract before coding:

```text
POST /api/products
Permission: Admin
Request: name, sku, price, dimensions, material
Response: id, name, sku, price, createdAt
Status: 201, 400, 401, 403, 409, 500
```

Clarify:

- Who can call it?
- What fields are required?
- What business rules can fail?
- Does it read, write, or both?
- Does it need transaction handling?
- Does it need cache read or invalidation?

### Step 2: Add Domain Objects

Add or update entities/value objects in `FurniSpace.Domain`.

Rules:

- Keep setters private.
- Use methods for state changes.
- Put core business checks in the domain.
- Raise domain events when meaningful.
- Do not inject services into entities.

### Step 3: Add DTOs, Commands, Queries

DTOs live in:

```text
FurniSpace.Application/DTOs
```

Commands/queries live in:

```text
FurniSpace.Application/Features/{Module}
```

Use clear names:

```text
CreateProductCommand
UpdateProductCommand
GetProductByIdQuery
GetProductsPagedQuery
```

Handlers should usually return:

```csharp
ServiceResult<TDto>
```

Use `PagedResult<T>` as the `Data` payload for paged endpoints.

### Step 4: Add Validation

Put validators next to the command/query they validate.

Validation belongs in Application when it checks request shape:

- Required fields.
- Length.
- Format.
- Range.
- Basic cross-field validation.

Domain invariants still belong in Domain.

### Step 5: Add Mapping

FurniSpace uses Mapster as the default mapper.

Mapping config lives in:

```text
FurniSpace.Application/Mappings
```

Rules:

- Map Domain entities to DTOs in Application.
- Do not expose Domain entities from controllers.
- Use explicit config for value objects, nested objects, renamed fields, and computed fields.
- Simple same-name mappings may use `.Adapt<T>()`.

Example:

```csharp
var dto = product.Adapt<ProductDto>();
return ServiceResult<ProductDto>.Success(dto);
```

### Step 6: Add Application Services

If the use case needs orchestration, define the service interface in Application and keep the service implementation in Application.

Examples:

```text
FurniSpace.Application/Interfaces/IProductService.cs
FurniSpace.Application/Services/ProductService.cs
FurniSpace.Application/Interfaces/IAuthService.cs
```

Application services should use Infrastructure repository/provider contracts, not EF Core/Redis/JWT SDKs directly.

### Step 7: Implement Infrastructure

Add implementation in Infrastructure:

```text
FurniSpace.Infrastructure/Repositories/IRepository/IProductRepository.cs
FurniSpace.Infrastructure/Repositories/Repository/ProductRepository.cs
FurniSpace.Infrastructure/Persistence/Configurations/ProductConfiguration.cs
FurniSpace.Infrastructure/Caching/RedisCacheService.cs
```

EF Core rules:

- Add `DbSet<T>` to `AppDbContext`.
- Add `IEntityTypeConfiguration<T>`.
- Use value object mapping where needed.
- Add migration when schema changes.

### Step 8: Register DI

Register Infrastructure dependencies from `Application.DependencyInjection`, then register Application services.

Infrastructure registration examples:

```csharp
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<ICacheService, RedisCacheService>();
```

Do not register Infrastructure services from Domain or API.

### Step 9: Add Controller

Controller responsibilities:

- Bind request.
- Call Application handler/service.
- Return `IActionResult`.

Controller should not:

- Query EF directly.
- Call Redis directly.
- Create JWTs directly.
- Execute business rules.

Use `BaseApiController.ToActionResult(...)` for `IServiceResult`.

### Step 10: Test

Minimum checks:

- `dotnet build FurniSpace.sln`
- Unit tests for Application/Domain logic.
- Infrastructure tests for repository/cache behavior when possible.
- API tests for route/status mapping when endpoint is stable.
- Manual Swagger/Postman/curl test for happy path and errors.

## 5. Results and Response Standard

Application handlers should return `ServiceResult<T>` for use cases that produce data.

Use:

```csharp
ServiceResult<T>.Success(data)
ServiceResult<T>.Created(data)
ServiceResult<T>.BadRequest(message)
ServiceResult<T>.NotFound(message)
ServiceResult<T>.Unauthorized(message)
ServiceResult<T>.Forbidden(message)
ServiceResult<T>.Conflict(message)
ServiceResult<T>.Failure(error)
```

Use `Error` when the failure needs a stable code:

```csharp
return ServiceResult<ProductDto>.Failure(
    Error.Conflict("Product.SkuExists", "SKU already exists"));
```

Use `PagedResult<T>` for paged data:

```csharp
var page = PagedResult<ProductDto>.Create(items, pageNumber, pageSize, totalItems);
return ServiceResult<PagedResult<ProductDto>>.Success(page);
```

HTTP status mapping:

| Case | Status |
| --- | --- |
| Success | `200` |
| Created | `201` |
| Validation/bad request | `400` |
| Unauthorized | `401` |
| Forbidden | `403` |
| Not found | `404` |
| Conflict | `409` |
| Unexpected error | `500` |

## 6. Redis and Cache Standard

Redis is used for temporary runtime state and read-model caching. PostgreSQL remains the source of truth.

Current base:

- Redis runs in Docker Compose as service `redis`.
- Redis password is loaded from `.env` through `REDIS_PASSWORD`.
- API reads `Redis__ConnectionString` from `.env`.
- Infrastructure registers `IConnectionMultiplexer`.
- Application uses `ICacheService`.
- Infrastructure implements `RedisCacheService`.
- Redis uses `noeviction` because it stores auth/session security keys.

Use Redis for:

- Read-heavy DTO/read-model cache.
- Short-lived catalog/project/user summaries.
- Dashboard counters.
- Login rate limiting.
- Refresh token/session state.
- JWT blacklist.
- Short-lived permission cache.

Do not use Redis for:

- Durable business data.
- Raw passwords.
- Raw refresh tokens.
- Raw OTP/reset tokens.
- EF Core tracked entities.
- Highly sensitive user data under shared keys.

### Cache Key Convention

General:

```text
furnispace:{module}:{resource}:{id}
furnispace:{module}:list:{hash}
```

Auth:

```text
furnispace:auth:refresh-token:{userId}:{refreshTokenHash}
furnispace:auth:blacklist:{jti}
furnispace:auth:login-attempt:{email}
furnispace:auth:otp:{email}
furnispace:auth:password-reset:{userId}:{tokenId}
furnispace:auth:permissions:{userId}
```

Rules:

- Always set a TTL unless there is a very clear reason not to.
- Cache DTOs/read models only.
- Prefer exact key deletion.
- Use prefix invalidation only for list/search caches.
- Never store raw tokens or secrets as keys or values.

### Cache Read Pattern

```csharp
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
```

### Cache Invalidation Pattern

After writes:

```csharp
await _cache.RemoveAsync($"furnispace:user:{userId}", cancellationToken);
await _cache.RemoveByPrefixAsync("furnispace:users:list:", cancellationToken);
```

Invalidation rules:

- Update item -> remove item key.
- Create/update/delete item -> remove related list keys.
- Change permissions -> remove `furnispace:auth:permissions:{userId}`.
- Logout -> revoke refresh token and blacklist current access token.

See `docs/redis-cache-guide.md` for deeper Redis details.

## 7. Authentication and Authorization Base

Current auth base:

- JWT access token signed with HS256.
- JWT secret is loaded from `.env`.
- JWT secret must be at least 32 bytes after base64 decoding or UTF-8 conversion.
- Refresh token is generated with cryptographically secure random bytes.
- Refresh token is stored in Redis with TTL.
- Refresh token key hashes the token before using it in Redis.
- Access token revocation uses JWT `jti`.
- JWT bearer validation rejects tokens without `jti`.
- JWT bearer validation checks Redis blacklist.
- `SaveToken` is disabled.
- `POST /api/auth/logout` revokes refresh/access token state.

Use `IAuthService` for auth flows:

```csharp
Task<AuthResponseDto> CreateSessionAsync(...);
Task<AuthResponseDto?> RotateRefreshTokenAsync(...);
Task<bool> ValidateRefreshTokenAsync(...);
Task RevokeRefreshTokenAsync(...);
Task RevokeAccessTokenAsync(...);
Task<bool> IsAccessTokenRevokedAsync(...);
```

When implementing login:

1. Validate credentials against user repository/password hasher.
2. Load roles/permissions.
3. Call `CreateSessionAsync(...)`.
4. Return `AuthResponseDto`.

When implementing refresh token:

1. Validate expired access token principal if needed.
2. Resolve user id/email/full name/roles.
3. Call `RotateRefreshTokenAsync(...)`.
4. Return unauthorized if rotation returns `null`.

Authorization should use ASP.NET Core policies/roles. Redis may cache permission data briefly, but database remains the source of truth.

## 8. Environment and Secrets

`appsettings.json` is safe to push and should only contain non-secret defaults such as logging.

Runtime secrets/configuration should come from `.env`, deployment variables, or a secret manager.

Current `.env` style:

```env
ConnectionStrings__DefaultConnection=...
REDIS_PASSWORD=...
Redis__ConnectionString=redis:6379
JWT_SECRET=...
JwtSettings__SecretKey=...
```

Rules:

- Do not put JWT secrets in `appsettings.json`.
- Do not put Redis passwords in `appsettings.json`.
- Do not commit production `.env` values.
- Keep Redis private inside the Docker network unless local debugging requires exposing a port.

## 9. Feature Checklist

Use this for every new feature/API.

Planning:

- [ ] Define route, request, response, status codes.
- [ ] Define permission/policy.
- [ ] Identify business rules and failure cases.
- [ ] Decide if the endpoint needs caching.

Domain:

- [ ] Add/update entity or aggregate.
- [ ] Add/update value objects.
- [ ] Add domain methods for state changes.
- [ ] Add domain events if needed.

Application:

- [ ] Add DTOs.
- [ ] Add command/query.
- [ ] Add validator.
- [ ] Add handler/use case.
- [ ] Add Mapster mapping.
- [ ] Return `ServiceResult<T>`.
- [ ] Define interfaces for persistence/cache/external services.

Infrastructure:

- [ ] Implement repository/service interfaces.
- [ ] Add EF configuration.
- [ ] Add `DbSet<T>` if new entity.
- [ ] Add migration if schema changes.
- [ ] Add Redis keys and invalidation if caching.
- [ ] Register DI.

API:

- [ ] Add controller action.
- [ ] Use `ToActionResult(...)`.
- [ ] Add `[Authorize]` or policies where needed.
- [ ] Do not add business logic to controller.

Tests:

- [ ] Domain rules.
- [ ] Application handler behavior.
- [ ] Validation failures.
- [ ] Cache hit/miss/invalidation if applicable.
- [ ] Auth/authorization cases if protected.

## 10. Agent Implementation Rules

Agents working on this repo should follow these rules:

- Read the relevant existing files before editing.
- Keep changes scoped to the requested feature.
- Do not bypass the architecture for speed.
- Do not put EF/Redis/JWT logic in controllers.
- Do not put infrastructure concerns in Domain.
- Prefer existing project patterns over new abstractions.
- Use `ServiceResult<T>` for use case outputs.
- Use DTOs for API responses.
- Add or update docs when introducing a new convention.
- Run `dotnet test FurniSpace.sln --no-restore` after changes when restore is already complete.
- If adding packages, run `dotnet restore FurniSpace.sln` first.

## 11. Recommended Implementation Order

For the current backend, prioritize:

1. Complete user repository, EF configuration, and Unit of Work.
2. Implement password hashing and user registration/login.
3. Implement refresh-token endpoint using `RotateRefreshTokenAsync(...)`.
4. Add role/permission model and authorization policies.
5. Add cache read/invalidation to stable query/command handlers.
6. Add business modules:
   - Projects
   - Furniture catalog
   - 3D design data
   - Quotation/order
   - Production/delivery

## 12. Quick Feature Template

```text
Feature:
Route:
Permission:
Request:
Response:
Status codes:

Domain:
- Entity/aggregate:
- Value objects:
- Business rules:
- Domain events:

Application:
- DTO:
- Command/query:
- Validator:
- Handler:
- Mapping:
- Interfaces:

Infrastructure:
- Repository/service implementation:
- EF configuration:
- Migration:
- Redis cache keys:
- Redis invalidation:

API:
- Controller/action:
- Auth policy:
- Response mapping:

Tests:
- Domain:
- Application:
- Infrastructure:
- API:
```

