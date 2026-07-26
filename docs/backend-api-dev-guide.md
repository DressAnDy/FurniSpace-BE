# FurniSpace Backend API Developer Guide

This guide is the source of truth for backend structure and implementation in FurniSpace. It should match the current project architecture, not a generic clean-architecture template.

## 1. Architecture

FurniSpace currently uses a layered backend with Application depending on Infrastructure.

```text
API / Presentation
  -> Application
  -> Infrastructure   (startup/migration only)
  -> Shared

Application
  -> Infrastructure
  -> Domain

Infrastructure
  -> Domain

Domain
  -> no project dependencies
```

Project references:

| Project | References |
| --- | --- |
| `FurniSpace.API` | `Application`, `Infrastructure`, `Shared` |
| `FurniSpace.Application` | `Domain`, `Infrastructure` |
| `FurniSpace.Infrastructure` | `Domain` |
| `FurniSpace.Domain` | none |
| `FurniSpace.Shared` | independent utilities |

Rules:

- `Domain` must not reference `Application`, `Infrastructure`, or `API`.
- `Application` owns use-case orchestration, DTO mapping, validation, auth session logic, and JWT token creation.
- `Infrastructure` owns EF Core, PostgreSQL, migrations, repositories, Redis implementation, Elasticsearch implementation, and provider contracts.
- Repository contracts and repository implementations live in `Infrastructure`.
- `API` stays thin: controllers bind HTTP requests, call Application services, and map results to HTTP responses.
- `API` may reference `Infrastructure` for composition/startup concerns such as `AppDbContext` migration, but controllers must not use EF, Redis, or repositories directly.

## 2. Project Map

| Project | Responsibility |
| --- | --- |
| `FurniSpace.API` | Controllers, middleware, Swagger, authentication pipeline, app startup, auto migration |
| `FurniSpace.Application` | Services, service interfaces, DTOs, results, validation, Mapster mappings, auth/JWT |
| `FurniSpace.Domain` | Entities, value objects, enums, domain events, specifications, domain exceptions |
| `FurniSpace.Infrastructure` | `AppDbContext`, EF configuration, migrations, repositories, Redis, Elasticsearch, provider interfaces |
| `FurniSpace.Shared` | Cross-cutting helpers without business ownership |

## 3. Folder Conventions

### API

```text
FurniSpace.API/
  Controllers/
  Middleware/
  Filters/
  Base/
  Program.cs
```

Controllers should depend on Application interfaces such as:

```csharp
private readonly IAccountService _accounts;
private readonly IAuthService _authService;
```

Do not inject `AppDbContext`, repositories, Redis, Elasticsearch, or token services into controllers.

### Application

```text
FurniSpace.Application/
  Common/
    Auth/
    Results/
    ValidationBehavior.cs
  DTOs/
    Accounts/
  Interfaces/
    Accounts/
    Identity/
    External/
  Services/
    Accounts/
    Identity/
  Mappings/
  Features/
```

Current examples:

```text
Interfaces/Accounts/IAccountService.cs
Services/Accounts/AccountService.cs

Interfaces/Identity/IAuthService.cs
Interfaces/Identity/IJwtTokenService.cs
Interfaces/Identity/IRefreshTokenStore.cs
Services/Identity/AuthService.cs
Services/Identity/JwtTokenService.cs
Services/Identity/RefreshTokenStore.cs
Common/Auth/JwtSettings.cs
```

Application may depend on Infrastructure contracts, for example:

```csharp
using FurniSpace.Infrastructure.Repositories.IRepository;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;
```

Application must not contain EF Core query details, Redis commands, SQL, Elasticsearch client calls, or HTTP SDK implementation code.

### Infrastructure

```text
FurniSpace.Infrastructure/
  Data/
    AppDbContext.cs
    DataSeeder.cs
  Repositories/
    Base/
      IGenericRepository.cs
      GenericRepository.cs
    IRepository/
      IAccountRepository.cs
    Repository/
      AccountRepository.cs
  Interfaces/
    ICacheService.cs
    ISearchIndexService.cs
  Caching/
    RedisCacheService.cs
    RedisKeyBuilder.cs
  Search/
    ElasticsearchIndexService.cs
  Common/
    Caching/
    Search/
  Migrations/
  Persistence/
```

Repository convention:

- Generic base goes in `Repositories/Base`.
- Repository contracts go in `Repositories/IRepository`.
- Repository implementations go in `Repositories/Repository`.
- Repository contracts can expose domain entities because this project intentionally keeps repository ownership in Infrastructure.

Example:

```text
Repositories/IRepository/IAccountRepository.cs
Repositories/Repository/AccountRepository.cs
```

### Domain

```text
FurniSpace.Domain/
  Entities/
  Enums/
  ValueObjects/
  Events/
  Specifications/
  Exceptions/
  Common/
```

Do not put repository interfaces in `Domain`. In this project, repositories belong to `Infrastructure`.

## 4. Request Flow

Normal request flow:

```text
HTTP request
  -> API Controller
  -> Application service
  -> Domain entity/value object
  -> Infrastructure repository/provider contract
  -> Infrastructure implementation
  -> PostgreSQL/Redis/Elasticsearch/external provider
  -> DTO
  -> ServiceResult<T>
  -> HTTP response
```

Layer ownership:

| Logic | Layer |
| --- | --- |
| Route binding, auth middleware, HTTP status mapping | API |
| Use-case orchestration | Application |
| Request validation | Application |
| DTO mapping | Application |
| JWT creation and refresh-token orchestration | Application |
| Entity invariants and domain behavior | Domain |
| EF queries and writes | Infrastructure |
| Redis commands and cache serialization | Infrastructure |
| Elasticsearch client calls | Infrastructure |
| Repository contracts and implementations | Infrastructure |

## 5. Results and HTTP Responses

Use `ServiceResult<T>` for Application service outputs.

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

Use `PagedResult<T>` for paged responses.

Controllers should return through `BaseApiController.ToActionResult(...)`.

## 6. Authentication and JWT

Auth/JWT is owned by Application.

```text
FurniSpace.Application/Common/Auth/JwtSettings.cs
FurniSpace.Application/Interfaces/Identity/IAuthService.cs
FurniSpace.Application/Interfaces/Identity/IJwtTokenService.cs
FurniSpace.Application/Interfaces/Identity/IRefreshTokenStore.cs
FurniSpace.Application/Services/Identity/AuthService.cs
FurniSpace.Application/Services/Identity/JwtTokenService.cs
FurniSpace.Application/Services/Identity/RefreshTokenStore.cs
```

Current auth rules:

- JWT access tokens are signed with HS256.
- JWT secret is loaded from `.env` via `JWT_SECRET` or `JwtSettings__SecretKey`.
- JWT secret must be at least 32 bytes after base64 decoding or UTF-8 conversion.
- Access tokens must contain `jti`.
- JWT bearer validation rejects revoked access tokens by checking `IAuthService`.
- Refresh tokens are generated with secure random bytes.
- Refresh token storage uses Redis through `Infrastructure.Interfaces.ICacheService`.
- Raw refresh tokens must never be stored as Redis keys or values.

Use `IAuthService` for auth flows:

```csharp
Task<AuthResponseDto> CreateSessionAsync(...);
Task<AuthResponseDto?> RotateRefreshTokenAsync(...);
Task<bool> ValidateRefreshTokenAsync(...);
Task RevokeRefreshTokenAsync(...);
Task RevokeAccessTokenAsync(...);
Task<bool> IsAccessTokenRevokedAsync(...);
```

Email verification and password-reset messages use the Gmail API over HTTPS:

```text
FurniSpace.Infrastructure/Interfaces/IEmailService.cs
FurniSpace.Infrastructure/Common/Email/Settings/GmailApiSettings.cs
FurniSpace.Infrastructure/Common/Email/Services/GmailAccessTokenProvider.cs
FurniSpace.Infrastructure/Common/Email/Services/GmailApiEmailService.cs
```

Required configuration:

```text
GmailApi__ClientId
GmailApi__ClientSecret
GmailApi__RefreshToken
GmailApi__SenderEmail
GmailApi__SenderName
GmailApi__ResetPasswordUrl
```

Optional configuration:

```text
GmailApi__BaseUrl=https://gmail.googleapis.com/gmail/v1/
GmailApi__TokenUrl=https://oauth2.googleapis.com/token
GmailApi__TimeoutSeconds=10
```

Gmail API deployment rules:

- Enable Gmail API and grant only `https://www.googleapis.com/auth/gmail.send`.
- Store the OAuth client secret and refresh token only in local secrets or Render environment variables; never commit them.
- `GmailApi__SenderEmail` must be the Gmail account that authorized the refresh token.
- Gmail access tokens are refreshed and cached automatically. If the OAuth app remains in Testing, the refresh token may expire after seven days.
- Gmail API uses HTTPS port 443 and therefore does not depend on SMTP egress from Render.
- If Register commits the account but email delivery fails, return `201 Created` with `EmailDeliveryStatus = "failed"` and let the client call resend OTP.
- Resend OTP and forgot-password responses remain neutral so callers cannot infer whether an account exists.
- Revoke and regenerate OAuth credentials immediately if a client secret or refresh token is exposed.
- Do not log OAuth credentials, access tokens, OTP codes, reset tokens, recipient addresses, or email bodies.

## 7. Redis and Cache

Redis implementation belongs to Infrastructure.

```text
FurniSpace.Infrastructure/Interfaces/ICacheService.cs
FurniSpace.Infrastructure/Caching/RedisCacheService.cs
FurniSpace.Infrastructure/Caching/RedisKeyBuilder.cs
FurniSpace.Infrastructure/Common/Caching/RedisSettings.cs
```

Redis is used for temporary runtime state:

- Read-model cache.
- Refresh token/session state.
- JWT blacklist.
- Login attempts.
- OTP/password reset state if added later.
- Short-lived permission cache.

Redis rules:

- PostgreSQL remains the source of truth.
- Always set TTL unless there is a clear reason not to.
- Cache DTOs/read models, not EF tracked entities.
- Do not store raw passwords, raw refresh tokens, raw OTPs, or raw reset tokens.
- Keep Redis private inside Docker/network boundaries.
- Use `noeviction` for Redis instances that store auth/session security keys.

Auth cache keys:

```text
furnispace:auth:refresh-token:{userId}:{refreshTokenHash}
furnispace:auth:blacklist:{jti}
furnispace:auth:login-attempt:{email}
furnispace:auth:otp:{email}
furnispace:auth:password-reset:{userId}:{tokenId}
furnispace:auth:permissions:{userId}
```

## 8. Database and Migrations

EF Core belongs to Infrastructure.

```text
FurniSpace.Infrastructure/Data/AppDbContext.cs
FurniSpace.Infrastructure/Migrations/
FurniSpace.Infrastructure/Persistence/Configurations/
```

`Program.cs` currently applies migrations at startup:

```csharp
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Failed to apply database migrations during startup.");
    }
}
```

This is a startup/composition concern. Controllers must still avoid `AppDbContext`.

Useful commands:

```powershell
dotnet ef migrations list --project src\FurniSpace.Infrastructure\FurniSpace.Infrastructure.csproj --startup-project src\FurniSpace.API\FurniSpace.API.csproj
dotnet ef database update --project src\FurniSpace.Infrastructure\FurniSpace.Infrastructure.csproj --startup-project src\FurniSpace.API\FurniSpace.API.csproj
```

## 9. Adding a Feature

Use this order for a new API feature.

1. Define endpoint contract:

```text
Route:
Permission:
Request:
Response:
Status codes:
```

2. Add or update Domain entities/value objects if business state changes.

3. Add Application DTOs:

```text
FurniSpace.Application/DTOs/{Module}/
```

4. Add Application service interface and service implementation:

```text
FurniSpace.Application/Interfaces/{Module}/I{Module}Service.cs
FurniSpace.Application/Services/{Module}/{Module}Service.cs
```

5. Add Infrastructure repository contract and implementation if persistence is needed:

```text
FurniSpace.Infrastructure/Repositories/IRepository/I{Entity}Repository.cs
FurniSpace.Infrastructure/Repositories/Repository/{Entity}Repository.cs
```

6. Add EF config/`DbSet<T>`/migration if schema changes.

7. Register dependencies:

```csharp
// Infrastructure/DependencyInjection.cs
services.AddScoped<IProductRepository, ProductRepository>();

// Application/DependencyInjection.cs
services.AddScoped<IProductService, ProductService>();
```

8. Add API controller action and return via `ToActionResult(...)`.

9. Run verification:

```powershell
dotnet restore FurniSpace.sln
dotnet build FurniSpace.sln --no-restore
dotnet test FurniSpace.sln --no-build
```

## 10. Feature Checklist

Planning:

- [ ] Route, request, response, status codes are defined.
- [ ] Permission/policy is defined.
- [ ] Business rules and failure cases are identified.
- [ ] Cache usage is decided.

Domain:

- [ ] Entity/value object changes are added if needed.
- [ ] Domain invariants stay in Domain.

Application:

- [ ] DTOs are added.
- [ ] Service interface is under `Interfaces/{Module}`.
- [ ] Service implementation is under `Services/{Module}`.
- [ ] Mapping is under `Mappings`.
- [ ] Results use `ServiceResult<T>`.
- [ ] Auth/JWT logic stays in `Services/Identity`.

Infrastructure:

- [ ] Repository interface is under `Repositories/IRepository`.
- [ ] Repository implementation is under `Repositories/Repository`.
- [ ] EF configuration and `DbSet<T>` are added if needed.
- [ ] Migration is added if schema changes.
- [ ] Redis/search provider code stays in Infrastructure.

API:

- [ ] Controller is thin.
- [ ] Uses Application services only for request handling.
- [ ] Uses `ToActionResult(...)`.
- [ ] Adds `[Authorize]` or policies where needed.

Tests:

- [ ] Domain rules.
- [ ] Application service behavior.
- [ ] Validation failures.
- [ ] Infrastructure repository/cache behavior when practical.
- [ ] API route/status mapping when endpoint is stable.

## 11. Catalog Business Type Documentation

Business Type is a Catalog concept used to describe which business environments a Product is suitable for,
for example cafe, restaurant, fashion store, showroom, salon, or pharmacy. It is separate from Category:

- Category answers what the furniture is, for example counter, table, chair, shelf, or lighting.
- Business Type answers where the furniture is intended to be used.
- `projects.business_type` remains customer-entered free text from project requests and is not linked to Catalog Business Type.

### DBML

The Catalog DBML should include `business_types` and `products.business_type_ids`.
There is intentionally no join table for Product and Business Type.

```dbml
Table business_types {
  id integer [pk, increment]
  code varchar(50) [not null, unique]
  name varchar(150) [not null]
  description varchar(500)
  status boolean [not null, default: true]
  created_at timestamptz [not null]
  updated_at timestamptz [not null]
}

Table products {
  product_id uuid [pk]
  category_id uuid
  business_type_ids integer[] [note: 'Nullable PostgreSQL int array. No DB-level FK exists for array elements.']
  product_code varchar(50)
  product_name varchar(150) [not null]
  description text
  status product_status
  created_at timestamptz
  updated_at timestamptz
}

Ref: products.category_id > categories.category_id
```

Do not add a DBML `Ref` from `products.business_type_ids` to `business_types.id`.
PostgreSQL cannot enforce an array-element foreign key with a normal FK constraint.
The database uses a nullable `integer[]` column plus a GIN index for lookup performance.

`ProductService` owns validation for this relationship:

- create/update normalizes duplicate `businessTypeIds`.
- ID values must be positive.
- referenced Business Types must exist.
- referenced Business Types must be active.
- missing IDs return validation errors through `ServiceResult<T>`.

### Business Type API

Business Type routes live in the Catalog module.

| Route | Access | Description |
| --- | --- | --- |
| `GET /business-types` | Public | List active/inactive Business Types with pagination/filtering. |
| `GET /business-types/{id}` | Public | Get Business Type detail. |
| `POST /business-types` | Admin | Create a Business Type. |
| `PATCH /business-types/{id}` | Admin | Update Business Type code/name/description/status. |
| `PATCH /business-types/{id}/status` | Admin | Update active/inactive status. |

### Product Request And Response Changes

Product create/update requests may include:

```json
{
  "categoryId": "uuid",
  "businessTypeIds": [1, 2],
  "productCode": "PM-COUNTER-001",
  "productName": "Coffee Counter",
  "description": "Counter template for cafe projects"
}
```

Product list/detail/search responses include both the raw IDs and resolved Business Type summaries:

```json
{
  "productId": "uuid",
  "categoryId": "uuid",
  "categoryName": "Counter",
  "businessTypeIds": [1, 2],
  "businessTypes": [
    {
      "id": 1,
      "code": "CAFE",
      "name": "Cafe",
      "status": true
    }
  ]
}
```

`businessTypeIds` is nullable on Product:

- `null` means no Business Type assignment has been stored.
- `[]` means the Product has explicitly no Business Type assignment.
- when a Business Type filter is used, both `null` and `[]` do not match.
- when no Business Type filter is used, existing Product list/search behavior is unchanged.

### Product Filtering

`GET /products` and `GET /products/search` support repeated query parameters:

```http
GET /products?businessTypeIds=1&businessTypeIds=2
GET /products/search?businessTypeIds=1&businessTypeIds=2
```

Filter semantics are ANY:

```text
Product matches when product.business_type_ids contains 1 OR 2.
```

PostgreSQL equivalent:

```sql
products.business_type_ids && ARRAY[1, 2]
```

Rules:

- filter is optional.
- duplicate query IDs are normalized before repository/search execution.
- IDs less than or equal to zero return `400 INVALID_BUSINESS_TYPE_FILTER`.
- Product with `business_type_ids IS NULL` does not match a filter.
- Product with `business_type_ids = '{}'` does not match a filter.
- unknown positive IDs currently return an empty result set for filtering.

### Elasticsearch Impact

`ProductSearchDocument` includes `businessTypeIds`.
`GET /products/search` adds a `terms` filter on `businessTypeIds` when the query contains Business Type IDs.
The PostgreSQL fallback in `ProductRepository.SearchPublicAsync` must apply the same ANY semantics so ES and fallback results stay equivalent.

After changing the Product search document or mapping, rebuild the Product index:

```powershell
dotnet run --project src/FurniSpace.API -- reindex products
```

## 12. Logging

Serilog configuration belongs in:

```text
FurniSpace.Infrastructure/Logging/SerilogConfiguration.cs
```

Logging middleware belongs in:

```text
FurniSpace.API/Middleware/CorrelationIdMiddleware.cs
FurniSpace.API/Middleware/RequestLoggingMiddleware.cs
FurniSpace.API/Middleware/ExceptionHandlingMiddleware.cs
```

Logging behavior:

- Development writes readable text to console and `logs/furnispace-YYYYMMDD.log`.
- Other environments write structured JSON to console and `logs/furnispace-YYYYMMDD.json`.
- Log events include `Application`, `CorrelationId`, and `TraceId`.
- Authenticated request logs include `UserId`.
- HTTP completion logs include `EventType`, `RequestMethod`, `RequestPath`, `StatusCode`, and `ElapsedMs`.
- HTTP `4xx` responses and requests taking at least one second log at `Warning`.
- HTTP `5xx` responses log at `Error`.
- Unhandled exceptions are logged once by `ExceptionHandlingMiddleware`.
- Error responses include the correlation ID for support and investigation.

Use structured message templates:

```csharp
logger.LogInformation(
    "Account {AccountId} was created by user {ActorUserId}",
    accountId,
    actorUserId);
```

Do not use string interpolation because it discards searchable properties:

```csharp
logger.LogInformation($"Account {accountId} was created");
```

Never log passwords, access tokens, refresh tokens, OTPs, reset tokens, authorization headers,
connection strings, or full request bodies containing sensitive data.

## 13. Agent Rules

- Read existing files before editing.
- Keep changes scoped to the request.
- Follow current project folders and namespaces.
- Do not move repository interfaces into Domain.
- Do not put EF/Redis/Elasticsearch code in Application services.
- Do not put business rules in controllers or Infrastructure.
- Use DTOs for API responses.
- Use `ServiceResult<T>` for Application service outputs.
- Update docs when changing architecture or conventions.
- Prefer `rg` for searching.
- Run build/tests after code changes when possible.
