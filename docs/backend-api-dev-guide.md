# FurniSpace Backend API Developer Guide

Source of truth for backend structure and implementation. Prefer this document over generic clean-architecture templates when the two disagree.

Related guides (deeper topics):

| Doc | When to use |
| --- | --- |
| `docs/api-reference.md` | Full REST + SignalR API reference (request / response) |
| `docs/integration-test-build-guide.md` | Testcontainers, Core suite, fixtures |
| `docs/integration-test-remaining-suites.md` | Handoff for Suites E–J + Mongo external work |
| `docs/payment-service-guide.md` | Deposit / PayOS / SePay flows |
| `docs/redis-cache-guide.md` | Cache keys and TTL details |
| `docs/signalr-notification-guide.md` | Realtime hubs and groups |
| `docs/firebase-storage-service-guide.md` | File upload / Firebase |
| `docs/mongodb-room-planner-guide.md` | Room planner scenes |
| `docs/elasticsearch-docker-guide.md` | Search / reindex locally |

---

## 1. Architecture

Layered backend with **Application depending on Infrastructure** (intentional; not strict clean architecture).

```text
API
  -> Application
  -> Infrastructure   (startup / migration composition only)
  -> Shared

Application
  -> Domain
  -> Infrastructure
  -> Shared

Infrastructure
  -> Domain
  -> Shared

Domain
  -> no project dependencies
```

| Project | References |
| --- | --- |
| `FurniSpace.API` | Application, Infrastructure, Shared |
| `FurniSpace.Application` | Domain, Infrastructure, Shared |
| `FurniSpace.Infrastructure` | Domain, Shared |
| `FurniSpace.Domain` | none |
| `FurniSpace.Shared` | none (`EnvLoader`, shared helpers) |

Rules:

- `Domain` must not reference Application, Infrastructure, or API.
- **Repository contracts and implementations live in Infrastructure** (not Domain).
- Application owns use-case orchestration, DTOs, mapping (Mapster), validation helpers, JWT/session orchestration.
- Infrastructure owns EF Core, PostgreSQL, migrations, repositories, Redis, Elasticsearch, Mongo, Gmail, Firebase.
- API stays thin: bind HTTP, authorize, call Application services, map `ServiceResult` → HTTP. Controllers must not use EF, Redis, or repositories.

There is **no MediatR / FluentValidation pipeline**. Validation is Application-side checks plus API `ValidationFilter`.

---

## 2. Solution map

```text
src/
  FurniSpace.API/
  FurniSpace.Application/
  FurniSpace.Domain/
  FurniSpace.Infrastructure/
  FurniSpace.Shared/
tests/
  UnitTests/
    FurniSpace.UnitTests.sln
    FurniSpace.*.Tests/
  IntegrationTests/
    FurniSpace.IntegrationTests.sln
    FurniSpace.*.IntegrationTests/    # Testcontainers / WebApplicationFactory
    FurniSpace.Testing/               # shared fixtures, fakes, seeders
docs/
```

Solutions:

- `tests/UnitTests/FurniSpace.UnitTests.sln`: source projects + four `*.Tests` projects; no Docker required.
- `tests/IntegrationTests/FurniSpace.IntegrationTests.sln`: source projects + `FurniSpace.Testing` + three `*.IntegrationTests` projects; Docker required.
- `FurniSpace.sln`: full meta-solution for IDE navigation and full builds.

| Project | Responsibility |
| --- | --- |
| `FurniSpace.API` | Controllers, middleware, hubs, Swagger, JWT pipeline, startup |
| `FurniSpace.Application` | Services, interfaces, DTOs, Mapster, auth/JWT stores orchestration |
| `FurniSpace.Domain` | Entities, enums, domain primitives under `Common/` |
| `FurniSpace.Infrastructure` | `AppDbContext`, migrations, repositories, Redis/ES/Mongo/email/storage |
| `FurniSpace.Shared` | Cross-cutting helpers without business ownership |
| `FurniSpace.Testing` | Postgres Testcontainer, Respawn, scenario seeders, API fakes |

---

## 3. Folder conventions

### API

```text
FurniSpace.API/
  Controllers/
    Admin/          AccountsController
    Auth/           AuthController  (route: auth)
    Catalog/        BusinessTypes, Categories, Products, ProductVersions, preview files
    Chat/           ProjectChats, ProjectChatMessages, status
    Payments/       Payments, webhooks (PayOS/SePay), admin/test helpers
    Production/     ProductionRequests, Items, Staff, customization
    Projects/       Projects, Proposals, Quotations, Orders, Areas, Schedules,
                    Files, Payments, CustomizationRequests, RoomPlannerScenes
    Shared/         Files, Notifications
  Middleware/       CorrelationId, RequestLogging, ExceptionHandling
  Filters/          ValidationFilter
  Hubs/             NotificationsHub, ProjectChatHub, PaymentHub
  Base/             BaseApiController
  Program.cs
```

Inject Application interfaces only:

```csharp
private readonly IProjectService _projects;
private readonly IAuthService _auth;
```

Do **not** inject `AppDbContext`, repositories, Redis, or Elasticsearch into controllers.

### Application

```text
FurniSpace.Application/
  Common/           Auth, Results, Payments, Orders, Projects, Realtime, ...
  Constants/
  DTOs/{Module}/
  Interfaces/{Module}/
  Services/{Module}/
  Mappings/
  DependencyInjection.cs
```

Module folders are mirrored (`Interfaces/Projects` ↔ `Services/Projects` ↔ `DTOs/Projects`).

There is **no** `Features/` folder and **no** `ValidationBehavior.cs`.

Application may use Infrastructure contracts:

```csharp
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Interfaces; // ICacheService, IEmailService, ...
```

Application must not contain EF queries, Redis commands, SQL, Elasticsearch client calls, or HTTP SDK bodies for external providers (those stay in Infrastructure or thin Application adapters that call Infrastructure interfaces).

### Infrastructure

```text
FurniSpace.Infrastructure/
  Data/                 AppDbContext, DataSeeder, Mongo/
  Repositories/
    Base/               IGenericRepository, GenericRepository
    IRepository/        I{Entity}Repository
    Repository/         {Entity}Repository
  Persistence/          IUnitOfWork, UnitOfWork  (+ sparse Configurations/)
  ReadModels/           query/read shapes per module
  Caching/              RedisCacheService, RedisKeyBuilder
  Common/
    Caching/            RedisSettings
    Search/Elasticsearch/
    Email/
    Storage/            Firebase
    Mongo/
  Logging/              SerilogConfiguration
  Migrations/
  DependencyInjection.cs
```

Most EF mapping is **inline in `AppDbContext`**. `DbSet`s use `*Set` naming (`ProjectSet`, `OrderSet`, …).

### Domain

```text
FurniSpace.Domain/
  Entities/
  Enums/
  Exceptions/
  Specifications/
  Common/               BaseEntity, Result, Error, IDomainEvent, ValueObject, ...
```

Do not put repository interfaces in Domain.

---

## 4. Request pipeline and flow

### HTTP pipeline (`Program.cs`)

```text
ForwardedHeaders
  -> HTTPS redirection
  -> CorrelationIdMiddleware
  -> RateLimiter          (auth-public: 10/min/IP on public auth routes)
  -> CORS
  -> Authentication
  -> RequestLoggingMiddleware
  -> ExceptionHandlingMiddleware
  -> Authorization
  -> Controllers
  -> SignalR hubs
```

### Use-case flow

```text
HTTP
  -> Controller
  -> Application service
  -> Domain entity / enum rules
  -> Infrastructure repository or provider
  -> PostgreSQL / Redis / Elasticsearch / Mongo / Firebase / Gmail / PayOS|SePay
  -> DTO
  -> ServiceResult<T>
  -> BaseApiController.ToActionResult(...)
```

| Concern | Layer |
| --- | --- |
| Route binding, auth middleware, HTTP status mapping | API |
| Use-case orchestration, DTO mapping, business validation | Application |
| Entity invariants / enums | Domain |
| EF queries/writes, Redis, ES, Mongo, email, storage | Infrastructure |

---

## 5. Core business flows (technical)

These are the main end-to-end paths developers touch most often.

### 5.1 Project lifecycle

Primary types: `ProjectService`, `ProjectStatusTransitionEvaluator`, enum `ProjectStatus`.

```text
SUBMITTED
  -> IN_CONSULTATION / NEED_BASIC_INFORMATION
  -> WAITING_FOR_DESIGNER_ASSIGNMENT
  -> MEASUREMENT_REQUIRED / SPACE_VERIFIED
  -> PROPOSAL_CONSULTING -> PROPOSAL_SELECTED
  -> QUOTATION_SENT / QUOTATION_REVISION_REQUESTED
  -> ORDER_CONFIRMED
  -> IN_PRODUCTION / PRODUCTION_BLOCKED
  -> READY_FOR_DELIVERY -> DELIVERING -> DELIVERED -> COMPLETED
  (or REJECTED)
```

Roles (`ADMIN`, `CUSTOMER`, `DESIGNER`, `SALES`, `PRODUCTION`) gate who may transition. Customers do not update project status via the status API; designers have a restricted target set (see `ProjectStatusTransitionEvaluator`).

Related modules on the same project: proposals, room planner (Mongo), quotations, orders, payments, chat, files, customization / production.

### 5.2 Quotation → order

1. Quotation accepted (`QuotationService`) creates an `Order` with deposit/remaining amounts.
2. Defaults: `OrderWorkflow:DepositPercent` = **30** (override `ORDER_DEPOSIT_PERCENT`).
3. Order status starts around `DEPOSIT_PENDING` then progresses (`OrderStatus`).

`PaidAmount` / `RemainingAmount` live on **Order**, not on `Payment`.

### 5.3 Payments

Primary types: `PaymentService`, `PaymentBusinessEffectService`, PayOS/SePay webhook handlers.

- Payment entity tracks one collectable amount + `PaymentStatus` (`PENDING`, `PROCESSING`, `PAID`, `CANCELLED`, `EXPIRED`, `REFUNDED`).
- Types include `PROJECT_START_FEE`, `DEPOSIT`, `REMAINING_PAYMENT`, `FULL_PAYMENT`, …
- Project start fee default: `ProjectWorkflow:DefaultProjectStartFeeAmount` = **2_000_000** (override `PROJECT_START_FEE_AMOUNT`).
- Successful payment side effects recalculate order paid/remaining and may advance project/order state.

See `docs/payment-service-guide.md` for provider details.

### 5.4 Auth / identity

| Type | Owns |
| --- | --- |
| `IIdentityService` / `IdentityService` | Register, login, email OTP, forgot/reset password |
| `IAuthService` / `AuthService` | Session create/rotate, refresh validation, access-token blacklist |
| `IJwtTokenService` | Access token creation |
| Redis stores | Refresh tokens, OTP, password reset, blacklist |

Token delivery:

- HTTP: Authorization Bearer and/or cookies `access_token` / `refresh_token` (HttpOnly, Secure, SameSite=None).
- SignalR: query `access_token` allowed for hub paths under `/hubs/notifications` and `/hubs/project-chat`.

---

## 6. Results and HTTP responses

Use `ServiceResult<T>` (`FurniSpace.Application.Common`):

```csharp
ServiceResult<T>.Success(data)
ServiceResult<T>.Created(data)
ServiceResult<T>.BadRequest(message)          // optional field errors
ServiceResult<T>.NotFound(message)
ServiceResult<T>.Unauthorized(message)
ServiceResult<T>.Forbidden(message)
ServiceResult<T>.Conflict(message)
ServiceResult<T>.TooManyRequests(message)
ServiceResult<T>.PayloadTooLarge(message)
ServiceResult<T>.UnsupportedMediaType(message)
ServiceResult<T>.InternalServerError(message)
ServiceResult<T>.Failure(error)               // sets ErrorCode
```

Use `PagedResult<T>` for paged lists.

Controllers return through `BaseApiController.ToActionResult(...)`.

JSON enums use `JsonStringEnumConverter` (SCREAMING_SNAKE values in payloads).

API model validation failures go through `ValidationFilter` → `400` with `ServiceResult` shape (default ASP.NET model-state filter is suppressed).

---

## 7. Authentication and JWT

```text
Application/Common/Auth/JwtSettings.cs
Application/Interfaces/Identity/
Application/Services/Identity/
  AuthService.cs, JwtTokenService.cs, RefreshTokenStore.cs,
  EmailOtpStore.cs, PasswordResetStore.cs, IdentityService.cs
```

Rules:

- Access tokens: HS256; must include `jti`, subject, `iat`.
- Secret from `JwtSettings:SecretKey` / `JWT_SECRET` / `JwtSettings__SecretKey` (≥ 32 bytes after base64 or UTF-8).
- `OnTokenValidated` rejects revoked access tokens via `IAuthService.IsAccessTokenRevokedAsync`.
- Refresh tokens: secure random; stored hashed in Redis via `ICacheService` — never store raw refresh tokens as keys/values.
- Public auth endpoints are rate-limited (`auth-public`).

### Email (Gmail API)

```text
Infrastructure/Interfaces/IEmailService.cs
Infrastructure/Common/Email/...
```

Required env (typical):

```text
GmailApi__ClientId
GmailApi__ClientSecret
GmailApi__RefreshToken
GmailApi__SenderEmail
GmailApi__SenderName
GmailApi__ResetPasswordUrl
```

- Scope: `https://www.googleapis.com/auth/gmail.send` only.
- Never commit OAuth secrets; never log OTP, reset tokens, or email bodies.
- If register persists the account but email fails, return `201` with failed email delivery status and allow resend OTP.
- Resend OTP / forgot-password responses stay neutral (no account enumeration).

---

## 8. Redis and cache

```text
Infrastructure/Interfaces/ICacheService.cs
Infrastructure/Caching/RedisCacheService.cs
Infrastructure/Caching/RedisKeyBuilder.cs
Infrastructure/Common/Caching/.../RedisSettings.cs
```

Used for: refresh/session, JWT blacklist, login attempts, OTP, password reset, short-lived caches.

Rules:

- PostgreSQL is source of truth.
- Always set TTL unless there is a strong reason not to.
- Cache DTOs/read models, not tracked EF entities.
- Do not store raw passwords, refresh tokens, OTPs, or reset tokens.
- Prefer `noeviction` on Redis instances holding auth security keys.

Auth key patterns (see `docs/redis-cache-guide.md` for full list):

```text
furnispace:auth:refresh-token:{userId}:{refreshTokenHash}
furnispace:auth:blacklist:{jti}
furnispace:auth:login-attempt:{email}
furnispace:auth:otp:{email}
furnispace:auth:password-reset:{userId}:{tokenId}
```

---

## 9. Other infrastructure providers

Registered in `Infrastructure/DependencyInjection.cs` (called from `Application.AddApplication`):

| Provider | Config keys (examples) | Notes |
| --- | --- | --- |
| PostgreSQL | `ConnectionStrings:DefaultConnection` / `MigrationConnection`, env `__` forms | Npgsql enum mapping |
| Redis | `Redis:ConnectionString`, `REDIS_CONNECTION`, optional `REDIS_PASSWORD` | |
| Elasticsearch | `Elasticsearch:Url`, `ELASTICSEARCH_URL`, `InitializeIndices` | Indexers in Application/Search |
| MongoDB | `MongoDb:*`, `MONGODB_CONNECTION_STRING` | Room planner scenes |
| Firebase Storage | `FIREBASE_STORAGE_BUCKET`, credentials path | Project/product files |
| Gmail API | `GmailApi__*` | Transactional email |
| PayOS / SePay | Application options + env overrides | Webhooks under Payments controllers |

Reindex CLI (exits after run):

```powershell
dotnet run --project src/FurniSpace.API -- reindex products
# modules: accounts | products | projects | chat-messages | project-files
```

---

## 10. SignalR

| Hub | Path |
| --- | --- |
| `NotificationsHub` | `/hubs/notifications` |
| `ProjectChatHub` | `/hubs/project-chat` |
| `PaymentHub` | `/hubs/payments` |

API registers SignalR adapters that implement Application realtime interfaces. Prefer those interfaces from services; do not push hub logic into controllers. Details: `docs/signalr-notification-guide.md`.

---

## 11. Database and migrations

```text
Infrastructure/Data/AppDbContext.cs
Infrastructure/Migrations/
```

Startup (`Program.cs`):

- `StartupTasks:RunMigrations` (default **true**) → `MigrateAsync`
- `StartupTasks:SeedDemoData` (default **true**) → `DataSeeder.SeedAsync`
- Failures are logged; in **`IntegrationTest`** they **rethrow** (fail fast)

Commands:

```powershell
dotnet ef migrations list --project src\FurniSpace.Infrastructure\FurniSpace.Infrastructure.csproj --startup-project src\FurniSpace.API\FurniSpace.API.csproj
dotnet ef database update --project src\FurniSpace.Infrastructure\FurniSpace.Infrastructure.csproj --startup-project src\FurniSpace.API\FurniSpace.API.csproj
```

Controllers must still avoid `AppDbContext`.

---

## 12. Configuration and environments

- Prefer **section + env override** (`JwtSettings:SecretKey` / `JWT_SECRET`).
- Nested env uses `__` (`ConnectionStrings__DefaultConnection`).
- Root `.env` is loaded by `EnvLoader` unless environment is **`IntegrationTest`**.
- `appsettings.json` mainly holds logging; connection secrets come from env / `.env`.
- There is **no** `appsettings.IntegrationTest.json`. Integration fixtures set process env + in-memory config before host start.

Local day-to-day: keep `ASPNETCORE_ENVIRONMENT=Development`. Do not switch your personal `.env` to `IntegrationTest` to “run tests” — the test fixture sets that only inside the test process.

---

## 13. Adding a feature

1. Define contract: route, roles/policies, request/response, status codes, failure cases.
2. Domain entity/enum changes if state changes.
3. DTOs under `Application/DTOs/{Module}/`.
4. `Interfaces/{Module}/I{Module}Service.cs` + `Services/{Module}/{Module}Service.cs`.
5. Repository under `Infrastructure/Repositories/IRepository` + `Repository` if persistence is needed.
6. EF `DbSet` / mapping / migration if schema changes.
7. Register in `Infrastructure/DependencyInjection.cs` and/or `Application/DependencyInjection.cs`.
8. Thin controller action → `ToActionResult(...)`.
9. Tests: unit for service rules; Core integration for HTTP + Postgres when the path is P0.

Verify:

```powershell
dotnet restore tests/UnitTests/FurniSpace.UnitTests.sln
dotnet build tests/UnitTests/FurniSpace.UnitTests.sln --no-restore
dotnet test tests/UnitTests/FurniSpace.UnitTests.sln --no-build

# Docker required
dotnet test tests/IntegrationTests/FurniSpace.IntegrationTests.sln --filter "Category=Core"
```

---

## 14. Feature checklist

Planning:

- [ ] Route, request, response, status codes
- [ ] Roles/policies and failure cases
- [ ] Cache / realtime / search impact decided

Domain:

- [ ] Entity/enum changes only where needed
- [ ] Invariants stay in Domain (or clearly owned by Application if transitional)

Application:

- [ ] DTOs + service interface/implementation under module folders
- [ ] Mapster mapping updated
- [ ] Results use `ServiceResult<T>` / error codes
- [ ] Auth/session logic stays in Identity services

Infrastructure:

- [ ] Repository contract + implementation if needed
- [ ] Migration if schema changes
- [ ] Provider code (Redis/ES/Mongo/Firebase/email) stays in Infrastructure

API:

- [ ] Thin controller, Application services only
- [ ] `ToActionResult(...)`
- [ ] `[Authorize]` / roles where required

Tests:

- [ ] Unit coverage for business rules and validation failures
- [ ] Core integration for critical HTTP + persistence paths when stable

---

## 15. Catalog Business Type

Business Type describes **where** a product is intended (cafe, restaurant, …). Category describes **what** it is (table, chair, …).

- `projects.business_type` remains free-text on project requests and is **not** linked to catalog Business Types.
- Products store `business_type_ids` as nullable PostgreSQL `integer[]` (no join table, no FK on array elements). GIN index for lookup.
- `ProductService` validates IDs: positive, exist, active; duplicates normalized.

| Route | Access | Description |
| --- | --- | --- |
| `GET /business-types` | Public | List with pagination/filtering |
| `GET /business-types/{id}` | Public | Detail |
| `POST /business-types` | Admin | Create |
| `PATCH /business-types/{id}` | Admin | Update |
| `PATCH /business-types/{id}/status` | Admin | Active/inactive |

Product create/update may include `"businessTypeIds": [1, 2]`.

- `null` = no assignment stored; `[]` = explicitly none.
- Filters on `GET /products` and `GET /products/search` use **ANY** semantics (`&&` array overlap). Invalid IDs (≤ 0) → `400 INVALID_BUSINESS_TYPE_FILTER`.

`ProductSearchDocument` includes `businessTypeIds`. After mapping changes:

```powershell
dotnet run --project src/FurniSpace.API -- reindex products
```

---

## 16. Testing (quick reference)

| Suite | Projects | Notes |
| --- | --- | --- |
| Unit | `tests/UnitTests/FurniSpace.UnitTests.sln` → `*.Tests` | No Docker; CI + Sonar |
| Core integration | `*.IntegrationTests` + trait `Category=Core` | Postgres Testcontainers; Redis/ES/email/storage faked in API factory |
| Shared harness | `FurniSpace.Testing` | Excluded from Sonar coverage (test infra) |

```powershell
# All unit tests
dotnet test tests/UnitTests/FurniSpace.UnitTests.sln

# All Core integration tests (Docker required)
dotnet test tests/IntegrationTests/FurniSpace.IntegrationTests.sln --filter "Category=Core"
```

API integration uses `WebApplicationFactory`, test auth headers (`X-Test-User-Id`, `X-Test-Role`), Respawn reset before each test. Full details: `docs/integration-test-build-guide.md`.

CI (`.github/workflows/ci.yml`): build/test the unit solution, then build/test the Core integration solution. Sonar (`.github/workflows/build.yml`) builds and collects coverage from the unit solution only.

---

## 17. Logging

```text
Infrastructure/Logging/SerilogConfiguration.cs
API/Middleware/CorrelationIdMiddleware.cs
API/Middleware/RequestLoggingMiddleware.cs
API/Middleware/ExceptionHandlingMiddleware.cs
```

- Development: readable console + `logs/furnispace-YYYYMMDD.log`
- Other envs: structured JSON console + `.json` log file
- Enrich with `Application`, `CorrelationId`, `TraceId`; authenticated requests include `UserId`
- `4xx` / slow (≥1s) → Warning; `5xx` → Error
- Use structured templates; never interpolate secrets into messages
- Never log passwords, tokens, OTPs, connection strings, or sensitive bodies

---

## 18. Agent / contributor rules

- Read existing files before editing; keep changes scoped.
- Follow current folders and namespaces (no inventing `Features/` or Domain repositories).
- Do not move repository interfaces into Domain.
- Do not put EF/Redis/Elasticsearch client code in Application services.
- Do not put business rules in controllers.
- Use DTOs + `ServiceResult<T>` for Application outputs.
- Update this guide when architecture or conventions change.
- Prefer `rg` for searching; run build/tests after meaningful code changes.
