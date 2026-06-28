# FurniSpace Implementation Roadmap Plan

Tài liệu kế hoạch triển khai cho **3 nghiệp vụ kỹ thuật**:

1. **Integration Test**
2. **Rate Limiting**
3. **Data Consistency** (PostgreSQL ↔ Firebase Storage ↔ MongoDB tương lai)

> **Trạng thái codebase tại thời điểm lập plan:** ~533 unit tests; rate limit chỉ phủ auth public; consistency chưa thống nhất cross-store; chưa có integration test Postgres thật.

---

## Mục lục

- [1. Integration Test](#1-integration-test)
- [2. Rate Limiting](#2-rate-limiting)
- [3. Data Consistency](#3-data-consistency)
- [Thứ tự triển khai gợi ý](#thứ-tự-triển-khai-gợi-ý)
- [Phụ lục — File mới / sửa tổng hợp](#phụ-lục--file-mới--sửa-tổng-hợp)

---

# 1. Integration Test

## Nội dung (Content)

Integration test kiểm tra **nhiều layer làm việc cùng nhau** với dependency thật (chủ yếu PostgreSQL), thay vì fake toàn bộ như unit test hiện tại.

**Mục tiêu:**

- Bắt lỗi SQL, migration, index, enum PostgreSQL, transaction, FK.
- Xác nhận repository query (sort preview, catalog files) khớp production.
- (Phase sau) Xác nhận service orchestration và HTTP pipeline (`ValidationFilter`, JWT, routing).

**Không thay thế unit test** — chạy song song:

- Unit: nhanh, logic thuần, fake repository.
- Integration: chậm hơn, cần Docker/Testcontainers hoặc DB test riêng.

**Công nghệ đề xuất:** xUnit, Testcontainers.PostgreSql, shared fixture `FurniSpace.Testing`.

### Phạm vi module — **không chỉ Product**

Integration test áp dụng cho **toàn backend**, triển khai **theo phase / ưu tiên ROI**, không giới hạn một domain.

| Giai đoạn | Module được cover | Ghi chú |
|-----------|-------------------|---------|
| **Phase 1 (pilot)** | ProjectChat, ProjectFile, ProductVersion (repository) | `ProjectChatTestDataFactory` đã có sẵn — làm mẫu đầu tiên |
| **Phase 1 (mở rộng)** | Product, Account, Project, Category (repository) | Sort preview, catalog query, FK |
| **Phase 2** | Identity, Product preview service, Project accept/chat | Flow transaction + cross-store (fake Firebase) |
| **Phase 3** | Auth, Products, Projects, Files (HTTP smoke) | Không test hết controller ngay — smoke theo critical path |
| **Phase 5** | Proposal scene / Mongo | Khi có feature 3D |

**Product** được nhắc nhiều ở Phase 2 vì preview upload/reorder/delete vừa triển khai và dễ bắt bug SQL/transaction — **không có nghĩa chỉ test Product**. Các module Project, Identity, Chat vẫn nằm trong cùng roadmap.

### Vị trí Seed / DataFactory

Seed data **không** đặt trong `src/` (production). Cấu trúc đề xuất:

```text
tests/
  FurniSpace.Testing/                          ← shared test library (reference từ mọi *IntegrationTests)
    Fixtures/
      PostgresIntegrationFixture.cs            ← container + connection + migrate
      IntegrationCollectionDefinition.cs       ← xUnit collection fixture
    Infrastructure/
      DatabaseMigrationHelper.cs               ← MigrateAsync(AppDbContext)
    Seeding/
      IntegrationSeedContext.cs                ← optional: DbContext + metadata test run
      Core/
        RoleSeed.cs                              ← roles ADMIN, SALES, ...
        AccountSeed.cs                           ← customer, sales, designer, admin
      Modules/
        ProjectChatSeed.cs                       ← tách từ ProjectChatTestDataFactory hiện tại
        ProjectSeed.cs
        CatalogFileSeed.cs                       ← product + preview file_links
        ProductSeed.cs
      IntegrationDataFactory.cs                  ← facade: SeedMinimalAsync(), SeedProjectChatScenarioAsync(), ...
    Fakes/
      FakeFileStorageService.cs
      NoOpEmailService.cs

  FurniSpace.Infrastructure.IntegrationTests/
    Repositories/
      ProjectChatRepositoryIntegrationTests.cs   ← gọi IntegrationDataFactory, không seed inline

  FurniSpace.Application.IntegrationTests/     ← Phase 2
    ...                                          ← dùng cùng FurniSpace.Testing.Seeding

  FurniSpace.Infrastructure.Tests/               ← unit InMemory (giữ nguyên)
    ProjectChats/
      ProjectChatTestDataFactory.cs              ← Phase 0: refactor gọi lại Core/Modules seed
                                                   hoặc obsolete → redirect sang Testing
```

**Quy ước:**

| Loại | Đặt ở đâu | Ví dụ |
|------|-----------|--------|
| Seed **dùng chung** nhiều module | `FurniSpace.Testing/Seeding/Core/` | Role, Account |
| Seed **theo nghiệp vụ** | `FurniSpace.Testing/Seeding/Modules/` | ProjectChat, CatalogFile |
| **Facade** gom scenario | `IntegrationDataFactory.cs` | `SeedCatalogPreviewScenarioAsync()` |
| Seed **chỉ phục vụ 1 test file** | Cùng file test hoặc nested class | Tránh trừ khi thật sự one-off |
| Production seeder | `Infrastructure/Data/DataSeeder.cs` | **Không** dùng cho integration test |

**Luồng seed trong test:**

```text
PostgresIntegrationFixture (1 lần / collection)
  → MigrateAsync
  → IntegrationDataFactory.SeedXxxAsync(context)
  → Test gọi repository/service
  → (optional) Respawn / truncate giữa tests
```

**File hiện có:** `tests/FurniSpace.Infrastructure.Tests/ProjectChats/ProjectChatTestDataFactory.cs` sẽ **di chuyển logic** sang `FurniSpace.Testing/Seeding/Modules/ProjectChatSeed.cs` + `IntegrationDataFactory`; file cũ có thể giữ wrapper mỏng cho unit test InMemory hoặc reference trực tiếp Testing project.

---

## Chia phase

### Phase 0 — Nền móng (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 0.1 | Tạo `tests/FurniSpace.Testing/` — fixture Postgres, migrate helper, trait `Category=Integration` |
| 0.2 | Cập nhật `FurniSpace.sln` — thêm project test |
| 0.3 | Doc quy ước: unit vs integration, lệnh filter CI |
| 0.4 | Quyết định: project integration **tách riêng** hay folder trong `Infrastructure.Tests` (khuyến nghị: **tách riêng**) |

### Phase 1 — Repository + PostgreSQL

| Hạng mục | Mô tả |
|----------|--------|
| 1.1 | `PostgresIntegrationFixture` — Testcontainers spin Postgres 16, `MigrateAsync` |
| 1.2 | Seed → `FurniSpace.Testing/Seeding/` (xem mục **Vị trí Seed / DataFactory**); refactor `ProjectChatTestDataFactory` |
| 1.3 | Integration tests pilot: `ProjectChatRepository`, `ProjectFileRepository`, `ProductVersionRepository` — mở rộng Product/Project/Account sau |
| 1.4 | CI job mới — `ubuntu-latest` + Docker, `--filter Category=Integration` |

### Phase 2 — Application Service + DB (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 2.1 | Project `FurniSpace.Application.IntegrationTests` |
| 2.2 | DI: `AddApplication` + fake `IFileStorageService`, `IEmailService`, `IRealtimeNotificationService` |
| 2.3 | Flow ưu tiên: Identity register/verify, Product preview upload/reorder/delete, Project accept + chat |

### Phase 3 — API HTTP (`WebApplicationFactory`)

| Hạng mục | Mô tả |
|----------|--------|
| 3.1 | `public partial class Program` + env `SKIP_STARTUP_MIGRATION` |
| 3.2 | `CustomWebApplicationFactory`, `AuthTestHelper` |
| 3.3 | Smoke: login 401/200, `ValidationFilter` 400, authenticated CRUD |

### Phase 4 — External full stack (phase phụ, optional)

| Hạng mục | Mô tả |
|----------|--------|
| 4.1 | Testcontainers Redis (optional) |
| 4.2 | SignalR client test |
| 4.3 | Nightly CI job (không block mọi PR) |

### Phase 5 — MongoDB (khi có feature scene 3D)

| Hạng mục | Mô tả |
|----------|--------|
| 5.1 | Testcontainers MongoDB |
| 5.2 | Test Postgres `mongo_scene_id` ↔ Mongo document read/write/compensate |

---

## Input / Output

| Phase | Input | Output |
|-------|--------|--------|
| 0 | Solution hiện tại, Docker local/CI | Project structure, trait convention, CI filter command |
| 1 | Connection Postgres tạm, migrations EF | Repository tests pass trên PG thật; báo cáo CI xanh |
| 2 | Seed data, fake external services | Service flow tests pass; bug SQL/transaction được phát hiện |
| 3 | JWT test secret, `appsettings.Integration.json` | HTTP tests 200/401/400/429; pipeline auth + validation verified |
| 4 | Redis/SignalR config test | Realtime path smoke test |
| 5 | Mongo feature code | Cross-store scene consistency verified |

**Lệnh chạy (output vận hành):**

```bash
# Unit only (CI nhanh)
dotnet test --filter "Category!=Integration"

# Integration only (cần Docker)
dotnet test --filter "Category=Integration"
```

---

## Tác động đến codebase (nặng → nhẹ)

| Mức | Thành phần | Mô tả tác động |
|-----|------------|----------------|
| **Nặng** | `FurniSpace.API/Program.cs` | Phase 3: `partial Program`, skip startup migrate trong test |
| **TB** | `.github/workflows/ci.yml` | Job Docker + filter integration |
| **TB** | `tests/` (project mới) | `FurniSpace.Testing`, `*.IntegrationTests` |
| **Nhẹ** | `FurniSpace.Application/DependencyInjection.cs` | Optional overload cho test overrides |
| **Nhẹ** | `FurniSpace.Infrastructure/DependencyInjection.cs` | Optional public `AddPostgres(connectionString)` |
| **Nhẹ** | `ProjectChatTestDataFactory.cs` | Tách seed dùng chung InMemory + Postgres |
| **Không** | Domain, business logic production | Phase 0–1 không sửa `src/` |
| **Không** | Unit test hiện có (~533 tests) | Giữ nguyên, chạy song song |

**Service / module bị ảnh hưởng khi viết test (indirect — không refactor bắt buộc Phase 1):**

1. `ProjectFileRepository` — sort catalog/preview  
2. `ProjectChatRepository`, `ProjectChatMessageRepository`  
3. `ProductVersionRepository`  
4. `ProductPreviewImageService`, `ProductService`, `ProductVersionService` (Phase 2)  
5. `IdentityService`, `ProjectService` (Phase 2)  
6. Toàn bộ controllers (Phase 3)

---

## Testing phase

| Giai đoạn | Việc test |
|-----------|-----------|
| Sau Phase 0 | Build solution; project mới compile |
| Sau Phase 1 | ≥5 repository integration tests pass local + CI Docker |
| Sau Phase 2 | ≥3 service flows pass; không regression unit tests |
| Sau Phase 3 | ≥10 API smoke tests; auth + validation filter |
| Regression | Mọi PR: unit full suite; integration theo policy CI |
| Coverage | Integration có thể exclude khỏi Sonar gate ban đầu |

**Test case mẫu Phase 1:**

- Preview files sort: `PRODUCT_PREVIEW` → `display_order ASC` → `uploaded_at DESC`
- Reorder preview → query DB xác nhận thứ tự
- Migration enum PostgreSQL apply thành công

---

## Cần refactor gì để triển khai

| Hạng mục | Bắt buộc? | Ghi chú |
|----------|-----------|---------|
| Tách seed `ProjectChatTestDataFactory` | Khuyến nghị | Dùng chung InMemory unit + Postgres integration |
| `partial class Program` | Phase 3 | Bắt buộc cho `WebApplicationFactory` |
| Env `SKIP_STARTUP_MIGRATION` | Phase 3 | Tránh double migrate khi test host start |
| Fake `IFileStorageService` trong `FurniSpace.Testing` | Phase 2 | Tránh gọi Firebase thật |
| **Không** cần CQRS/MediatR | — | Service pattern hiện tại đủ |
| **Không** sửa `docker-compose.yml` dev | — | Testcontainers tự spin Postgres |

---

# 2. Rate Limiting

## Nội dung (Content)

Rate limiting bảo vệ API khỏi abuse: brute-force auth, scrape public endpoint, spam upload, poll notification.

**Hiện trạng đã có:**

| Lớp | Phạm vi | Chi tiết |
|-----|---------|----------|
| ASP.NET Rate Limiter | Policy `auth-public` | 10 req/phút/**IP** → HTTP 429 |
| | 7 endpoint | `POST /auth/register`, `verify-email`, `resend-verification-otp`, `login`, `refresh`, `forgot-password`, `reset-password` |
| Application (Redis/cache) | Theo **email** | `IdentityService.AllowEmailAttemptAsync`, window 5 phút |
| | Limits | register 5, verify 10, resend 3, login 10, forgot 3, reset 5 |

**Chưa có:** public read, upload multipart, change password, chat, SignalR, global default policy.

---

## Chia phase

### Phase 0 — Chuẩn hóa cấu hình hiện có (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 0.1 | Tách `AddPublicAuthRateLimiter` → `RateLimitingExtensions.cs` |
| 0.2 | Kiểm tra partition IP sau reverse proxy (`ForwardedHeaders` / `X-Forwarded-For`) |
| 0.3 | Document policy hiện có trong doc này + dev guide |

### Phase 1 — Public read (ưu tiên cao)

| Hạng mục | Mô tả |
|----------|--------|
| 1.1 | Policy `public-read`: 60 req/phút/IP |
| 1.2 | Gắn: `GET /products*`, `GET /categories`, `GET /files/by-reference` (**AllowAnonymous**) |

### Phase 2 — Upload multipart (ưu tiên cao)

| Hạng mục | Mô tả |
|----------|--------|
| 2.1 | Policy `upload`: 20 req/giờ/**user** (fallback IP nếu chưa auth) |
| 2.2 | Gắn: product files, preview files, product version files, project files, chat file messages |

### Phase 3 — Auth sensitive (ưu tiên TB)

| Hạng mục | Mô tả |
|----------|--------|
| 3.1 | Policy `auth-sensitive`: 5 req/15 phút/user |
| 3.2 | `PATCH /auth/me/password` |
| 3.3 | Application rate limit `refresh` theo userId (bổ sung IP limit hiện có) |

### Phase 4 — Write / chat / poll (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 4.1 | Policy `chat-write`: 60/phút/user — chat text + file |
| 4.2 | Policy `authenticated-write`: 30/phút/user — logout, project mutations |
| 4.3 | Policy `read-heavy`: notification list poll |

### Phase 5 — Config & SignalR (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 5.1 | `appsettings.RateLimiting` — permit/window configurable |
| 5.2 | Hub connect rate limit (`NotificationsHub`, `ProjectChatHub`) |
| 5.3 | `[DisableRateLimiting]` cho health/internal nếu cần |

### Phase 6 — Security gap kèm theo (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 6.1 | `AccountsController` POST/PUT/DELETE thiếu `[Authorize]` — fix trước/song song rate limit admin |

---

## Input / Output

| Phase | Input | Output |
|-------|--------|--------|
| 0 | `Program.cs` rate limit block | `RateLimitingExtensions.cs`, IP partition đúng sau proxy |
| 1–4 | Policy definitions + `[EnableRateLimiting]` trên controllers | HTTP 429 khi vượt ngưỡng; client nhận status rõ ràng |
| 3 | Redis cache (đã có) | Refresh/password abuse giảm |
| 5 | appsettings | Ops chỉnh limit không rebuild logic |
| Test | HttpClient gọi lặp endpoint | Assert 429 sau N requests |

**Response khi bị limit:**

- HTTP **429 Too Many Requests**
- (Optional Phase 5) Header `Retry-After`

---

## Tác động đến codebase (nặng → nhẹ)

| Mức | File / service | Mô tả |
|-----|----------------|--------|
| **TB** | `Program.cs` | Thay/add `AddFurniSpaceRateLimiting()` |
| **TB** | `Extensions/RateLimitingExtensions.cs` | **File mới** — toàn bộ policy |
| **Nhẹ** | Controllers (8 file) | Thêm `[EnableRateLimiting("...")]` |
| **Nhẹ** | `IdentityService` | Phase 3: rate limit refresh theo user |
| **Nhẹ** | `appsettings.json` | Section RateLimiting (Phase 5) |
| **Không** | Application business services khác | Không đổi logic nghiệp vụ |
| **Không** | Infrastructure repositories | — |

**Controllers bị tác động (theo thứ tự ưu tiên gắn policy):**

1. `AuthController` — thêm sensitive policies  
2. `FilesController` — `public-read` trên `by-reference`  
3. `ProductsController` — public GET + upload  
4. `ProjectFilesController`, `ProjectChatMessagesController` — upload  
5. `ProductVersionsController` — upload  
6. `CategoriesController` — public-read  
7. `ProjectChatMessagesController` — chat-write  
8. `NotificationsController` — read-heavy (optional)

---

## Testing phase

| Giai đoạn | Test |
|-----------|------|
| Unit | Mock `HttpContext` partition key helper (optional) |
| API | Gọi `/auth/login` 11 lần/IP → 429 (đã có policy, cần test assert) |
| Phase 1 | GET `/products` vượt 60/phút → 429 |
| Phase 2 | Upload liên tiếp → 429 theo user |
| Integration | Rate limit + auth cookie flow |
| Manual | Sau deploy proxy — verify IP thật không phải IP nội bộ proxy |

---

## Cần refactor gì để triển khai

| Hạng mục | Mô tả |
|----------|--------|
| Tách rate limit khỏi `Program.cs` | Tránh file Program phình to |
| Helper `FixedWindowByUserId` / `ByIp` | Tránh duplicate partition logic |
| Fix `AccountsController` authorization | Security prerequisite Phase 6 |
| **Không** cần package mới | `Microsoft.AspNetCore.RateLimiting` đã có trong .NET 8 |
| Optional | Custom `OnRejected` → `ServiceResult` shape thống nhất với `ValidationFilter` |

---

# 3. Data Consistency

## Nội dung (Content)

Data consistency đảm bảo **trạng thái đồng bộ** giữa các store và **transaction an toàn** trong PostgreSQL.

**Phạm vi FurniSpace:**

| Store | Vai trò |
|-------|---------|
| PostgreSQL | Source of truth metadata (EF Core) |
| Firebase Storage | File binary (preview, project files, catalog) |
| Redis | Cache, auth rate limit, OTP |
| MongoDB (tương lai) | Scene 3D payload (`MongoSceneId` trên Postgres) |

**Vấn đề hiện tại:**

- Error format lệch: `Error` + `errorCode` vs `BadRequest(string)`.
- Transaction helper lệch: `UnitOfWorkTransactions` vs 4 bản `ExecuteInTransactionAsync` private vs manual `Begin/Commit`.
- Cross-store upload/delete không thống nhất:
  - Preview upload: Storage → DB transaction + rollback Storage ✅
  - General catalog upload: Storage → DB **không** transaction, **không** rollback Storage ❌
  - Project file delete: xóa Storage **trước** DB ❌
- Notification: DB save rồi SignalR — eventual consistency có chủ đích.

---

## Chia phase

### Phase 0 — Quy ước & baseline (phase phụ)

| Hạng mục | Mô tả |
|----------|--------|
| 0.1 | Doc quy ước: upload Storage→DB+compensate; delete DB→Storage |
| 0.2 | Doc error code convention |
| 0.3 | Checklist PR: multi-entity? cross-store? transaction? |

### Phase 1 — Error / response consistency (nhẹ → TB)

| Hạng mục | Mô tả |
|----------|--------|
| 1.1 | Tạo `*ErrorCodes` cho module thiếu (ProjectFiles, Projects, Accounts…) |
| 1.2 | Chuyển `ServiceResult.BadRequest(string)` → `Failure(Error)` có code |
| 1.3 | (Phase phụ 1b) Client migrate đọc `errorCode` — breaking nếu parse message text |

### Phase 2 — Transaction helper thống nhất (nhẹ, nội bộ)

| Hạng mục | Mô tả |
|----------|--------|
| 2.1 | Xóa duplicate `ExecuteInTransactionAsync` private (4 service) |
| 2.2 | Dùng `UnitOfWorkTransactions` everywhere |
| 2.3 | `ProjectService` manual Begin/Commit → helper chung |
| 2.4 | (Phase phụ) Log warning nếu nested `BeginTransaction` |

### Phase 3 — Cross-store Firebase ↔ PostgreSQL (TB → nặng)

| Hạng mục | Mô tả |
|----------|--------|
| 3a | **Quick wins:** `ProductService.PersistUploadedFileAsync`, `ProductVersionService.PersistUploadedFileAsync` — transaction + rollback Storage |
| 3a | **Quick wins:** `ProjectFileService.DeleteFileAsync` — đảo thứ tự: DB trước, Storage sau |
| 3b | Extract `CatalogFileStorageOperations` / compensation helper dùng chung |
| 3b | Structured log khi compensate fail |
| 3c | (Phase phụ) `storage_cleanup_queue` + background retry xóa orphan |

### Phase 4 — Notification outbox (phase phụ, optional)

| Hạng mục | Mô tả |
|----------|--------|
| 4.1 | Outbox table + worker push SignalR |
| 4.2 | At-least-once delivery in-app + realtime |

### Phase 5 — Mongo polyglot (nặng, khi có feature)

| Hạng mục | Mô tả |
|----------|--------|
| 5.1 | Quy ước: Mongo write → Postgres transaction → compensate Mongo nếu fail |
| 5.2 | `ISceneDocumentStore` + orchestrator Application layer |
| 5.3 | Delete: Postgres unlink → async Mongo delete / queue |

---

## Input / Output

| Phase | Input | Output |
|-------|--------|--------|
| 0 | Team agreement | Written conventions in docs |
| 1 | Message strings hiện tại | Stable `errorCode` trên API responses |
| 2 | 6 service có transaction duplicate | Một pattern `UnitOfWorkTransactions` duy nhất |
| 3a | Upload/delete flows | Không orphan file Firebase / ghost DB metadata |
| 3b | Log/metrics | Ops thấy compensate failures |
| 5 | Scene CRUD feature | Postgres `mongo_scene_id` luôn trỏ document hợp lệ |

**Pattern output mong muốn (cross-store):**

```text
Upload:   Firebase → DB transaction → (fail) delete Firebase
Delete:   DB transaction → (success) delete Firebase → (fail) log/queue cleanup
Mongo:    Mongo → Postgres TX → (fail) delete Mongo
```

---

## Tác động đến codebase (nặng → nhẹ)

| Mức | Service / module | Phase | Mô tả |
|-----|------------------|-------|--------|
| **Nặng** | `ProductService`, `ProductVersionService` | 3a | `PersistUploadedFileAsync` + preview flows |
| **Nặng** | `ProjectFileService` | 3a | Upload/delete order |
| **TB** | `ProductPreviewImageService` | 3b | Gom compensation helper (đã khá tốt) |
| **TB** | `IdentityService`, `ProjectService`, `ProjectScheduleService`, `ProjectChatMessageService` | 2 | Transaction refactor |
| **TB** | Toàn bộ `*Service` trả lỗi | 1 | Error codes |
| **Nhẹ** | `UnitOfWorkTransactions.cs`, `UnitOfWork.cs` | 2 | Optional nested guard |
| **Nhẹ** | `NotificationDispatcher` | 4 | Outbox (optional) |
| **Nhẹ** | Scene services (chưa có) | 5 | Mongo orchestrator mới |
| **Không** | Domain entities | — | |
| **Không** | API controllers (Phase 1–3) | — | Trừ client đọc errorCode |

**Thứ tự service bị tác động nặng nhất:**

1. `ProjectFileService`  
2. `ProductService`  
3. `ProductVersionService`  
4. `ProductPreviewImageService`  
5. `ProjectService`  
6. `IdentityService`  
7. `ProjectChatMessageService`  
8. `ProjectScheduleService`  
9. Các service còn lại (Phase 1 error codes)

---

## Testing phase

| Giai đoạn | Test |
|-----------|------|
| Phase 1 | Unit/API assert `errorCode` field |
| Phase 2 | Regression toàn bộ unit tests (533+) — behavior không đổi |
| Phase 3a | Integration: upload DB fail → Storage object không tồn tại |
| Phase 3a | Integration: delete DB ok, Storage delete fail → log + manual cleanup doc |
| Phase 3 | Existing `ProductUploadFileServiceTests`, preview tests — mở rộng |
| Phase 5 | Mongo integration tests (Phase 5 Integration Test doc) |

---

## Cần refactor gì để triển khai

| Hạng mục | Phase | Mô tả |
|----------|-------|--------|
| Gom `ExecuteInTransactionAsync` duplicate | 2 | Mechanical refactor |
| `CatalogFileStorageOperations` helper | 3b | Upload/delete compensate dùng chung |
| `CatalogFileUploadResponseContext` / mapper | Done | Giữ pattern context record |
| Error codes per module | 1 | File `*ErrorCodes.cs` mới |
| **Không** CQRS/MediatR | — | Không cần cho consistency |
| Integration test infrastructure | 3a | Phụ thuộc Integration Test Phase 1–2 để verify cross-store |

---

# Thứ tự triển khai gợi ý

```text
Song song track A — Consistency (data risk cao)
  Consistency Phase 2 (transaction helper)
    → Consistency Phase 3a (cross-store quick wins)
    → Consistency Phase 1 (error codes, song song với client)

Song song track B — Integration test
  Integration Phase 0 → Phase 1 (Postgres repository)
    → Phase 2 (service) — verify Consistency 3a

Track C — Rate limit (độc lập, nhanh)
  Rate Limit Phase 0 → Phase 1 (public-read)
    → Phase 2 (upload) → Phase 3 (auth-sensitive)

Sau khi ổn định
  Integration Phase 3 (API)
  Consistency Phase 3b–5 / Rate Limit Phase 4–5
  Integration Phase 4–5 (Mongo)
```

**Không nên:** Mongo consistency (Phase 5) trước Firebase consistency (Phase 3a).

---

# Phụ lục — File mới / sửa tổng hợp

## File mới (ước lượng)

| Nghiệp vụ | Path |
|-----------|------|
| Integration | `tests/FurniSpace.Testing/Seeding/Core/`, `Modules/`, `IntegrationDataFactory.cs` |
| Integration | `tests/FurniSpace.Infrastructure.IntegrationTests/` |
| Integration | `tests/FurniSpace.Application.IntegrationTests/` (Phase 2) |
| Integration | `tests/FurniSpace.API.IntegrationTests/` (Phase 3) |
| Rate limit | `src/FurniSpace.API/Extensions/RateLimitingExtensions.cs` |
| Consistency | `src/FurniSpace.Application/Common/CatalogFileStorageOperations.cs` (Phase 3b) |
| Consistency | `src/FurniSpace.Application/DTOs/**/*ErrorCodes.cs` (Phase 1) |

## File sửa chính

| File | Integration | Rate limit | Consistency |
|------|:-----------:|:----------:|:-----------:|
| `FurniSpace.sln` | ✅ | — | — |
| `.github/workflows/ci.yml` | ✅ | — | — |
| `Program.cs` | ✅ Phase 3 | ✅ | — |
| `AuthController.cs` | Phase 3 test | ✅ | — |
| `FilesController.cs` | — | ✅ | — |
| `ProductsController.cs` | — | ✅ | — |
| `ProductService.cs` | Phase 2 test | — | ✅ |
| `ProductVersionService.cs` | Phase 2 test | — | ✅ |
| `ProjectFileService.cs` | Phase 2 test | ✅ upload | ✅ |
| `IdentityService.cs` | Phase 2 test | ✅ Phase 3 | Phase 2 TX |
| `ProjectService.cs` | Phase 2 test | — | Phase 2 TX |
| `UnitOfWorkTransactions.cs` | — | — | ✅ |

## Production code touch summary

| Nghiệp vụ | Phase đầu không sửa `src/` | Phase đầu sửa `src/` |
|-----------|---------------------------|----------------------|
| Integration Test | Phase 0–1 | Phase 3 (`Program`) |
| Rate Limit | — | Phase 0–2 (API layer) |
| Consistency | — | Phase 2–3 (Application) |

---

*Tài liệu này là living document — cập nhật khi hoàn thành từng phase hoặc thay đổi kiến trúc (Mongo, CQRS, v.v.).*
