# Hướng dẫn build Integration Test cho FurniSpace Backend

## 1. Mục tiêu

Tài liệu này mô tả cách chuẩn bị codebase, môi trường và lộ trình triển khai integration test cho FurniSpace Backend.

Tài liệu này bổ sung cho `docs/implementation-roadmap-plan.md`:

- `implementation-roadmap-plan.md` là roadmap tổng thể, đi từ repository integration đến Application service và cuối cùng là API HTTP.
- Tài liệu này chi tiết hóa cách build test harness, dependency strategy, HTTP workflow và tiêu chí vận hành.
- Khi triển khai, giữ thứ tự chung: shared testing foundation -> PostgreSQL repository -> Application service -> API HTTP -> external dependency.
- Nếu hai tài liệu khác nhau về phase hoặc cấu trúc project, roadmap tổng thể là nguồn quyết định thứ tự; tài liệu này là hướng dẫn kỹ thuật cho từng bước.

Mục tiêu của bộ test:

- Khởi động API thật trong test host và gửi HTTP request qua toàn bộ ASP.NET Core pipeline.
- Kiểm tra route, model binding, validation, authentication, authorization, middleware, Application service và persistence.
- Chạy migration trên PostgreSQL thật, không dùng EF Core InMemory để thay thế hành vi PostgreSQL.
- Kiểm tra Redis thật cho session, refresh token và JWT blacklist.
- Không gửi email, upload Firebase hoặc gọi cổng thanh toán thật.
- Tách các test cần Elasticsearch/MongoDB thành nhóm riêng để giữ bộ test cốt lõi nhanh và ổn định.
- Chạy được bằng một lệnh trên máy phát triển và trong CI, không phụ thuộc `.env` cá nhân.

## 2. Hiện trạng codebase

### 2.1 Kiến trúc liên quan

Luồng request hiện tại:

```text
HTTP
  -> FurniSpace.API
  -> FurniSpace.Application
  -> FurniSpace.Infrastructure
  -> PostgreSQL / Redis / Elasticsearch / MongoDB / external providers
```

Các điểm chính:

- `src/FurniSpace.API/Program.cs` đăng ký toàn bộ dependency, chạy migration và seed trước khi nhận request.
- `src/FurniSpace.Application/DependencyInjection.cs` gọi trực tiếp `AddInfrastructure`.
- `src/FurniSpace.Infrastructure/DependencyInjection.cs` đăng ký PostgreSQL, Redis, Elasticsearch, MongoDB, Gmail và Firebase cùng lúc.
- `DataSeeder` chứa bộ dữ liệu demo lớn, có ID cố định và giá trị thời gian dựa trên `now()`.
- Docker Compose đã có PostgreSQL 16, Redis 7, Elasticsearch 8 và MongoDB 8.

### 2.2 Test hiện có

Solution hiện có bốn test project:

- `FurniSpace.Domain.Tests`
- `FurniSpace.Application.Tests`
- `FurniSpace.Infrastructure.Tests`
- `FurniSpace.API.Tests`

`FurniSpace.API.Tests` hiện chủ yếu khởi tạo controller trực tiếp với fake service. Các test này hữu ích ở mức unit/controller test, nhưng chưa kiểm tra:

- ASP.NET Core routing và middleware pipeline.
- Authentication handler và role authorization thực tế.
- Dependency Injection graph khi host khởi động.
- EF Core migration, transaction và truy vấn PostgreSQL thực.
- Redis session/refresh-token/JWT blacklist.
- JSON serialization, cookie, header và HTTP response hoàn chỉnh.

Chưa có các thành phần thường dùng cho API integration test:

- `WebApplicationFactory<Program>`.
- Testcontainers hoặc một cơ chế cấp phát dependency cô lập tương đương.
- Database reset/fixture chuẩn.
- Test authentication handler.
- Integration-test job riêng trong CI.

### 2.3 Các trở ngại hiện tại

1. `Program` chưa được expose cho `WebApplicationFactory`.
2. Startup luôn chạy migration và seed; exception bị log rồi bỏ qua. Trong test, lỗi database phải làm host fail ngay để tránh kết quả sai.
3. `AddInfrastructure` đăng ký mọi provider cùng lúc. Elasticsearch initializer có thể chạy khi host start dù test không dùng search.
4. Redis connection được tạo từ cấu hình runtime; test hiện chưa có cách cấp phát instance riêng.
5. Demo seed không phù hợp làm dữ liệu test độc lập vì lớn, dùng thời gian động và tạo trạng thái dùng chung.
6. Các provider ra Internet gồm Gmail API, Firebase và PayOS phải được thay bằng fake/stub.
7. CI hiện chỉ chạy bốn project test hiện tại và chưa khởi tạo dependency integration-test.

## 3. Chiến lược đề xuất

### 3.1 Tách core suite và external-dependency suite

**Core Integration Tests**

- Repository và Application test chạy với PostgreSQL thật.
- API test chạy bằng `WebApplicationFactory`.
- PostgreSQL thật trong container.
- Redis chưa nằm trong core suite giai đoạn đầu; workflow không kiểm tra auth/session dùng fake/no-op.
- Redis Testcontainer được bổ sung sau cho nhóm auth/session/refresh-token/JWT-blacklist.
- Gmail, Firebase, PayOS và realtime side effects dùng fake/no-op.
- Elasticsearch và MongoDB được tắt hoặc thay bằng no-op nếu scenario không cần chúng.
- Đây là nhóm bắt buộc trên mọi pull request.

**External Dependency Integration Tests**

- Elasticsearch thật cho search/index.
- MongoDB thật cho room-planner scene.
- Có thể thêm contract test cho HTTP provider bằng local stub server.
- Chạy ở job riêng hoặc theo trait/category vì nặng hơn.

Không nên đưa Gmail, Firebase hoặc PayOS thật vào CI integration test. Việc gọi hệ thống thật tạo dữ liệu ngoài ý muốn, cần secret và làm test không ổn định.

### 3.2 Tạo test project riêng

Đề xuất thêm:

```text
tests/
  FurniSpace.Testing/
    Fixtures/
      PostgresIntegrationFixture.cs
      RedisIntegrationFixture.cs        # bổ sung ở phase auth/session sau
      IntegrationCollectionDefinition.cs
    Infrastructure/
      DatabaseMigrationHelper.cs
      DatabaseReset.cs
    Fakes/
      FakeEmailService.cs
      FakeFileStorageService.cs
      FakePayOsClient.cs
      NoOpSearchIndexService.cs
    Seeding/
      Core/
      Modules/
      IntegrationDataFactory.cs

  FurniSpace.Infrastructure.IntegrationTests/
    Repositories/

  FurniSpace.Application.IntegrationTests/
    Identity/
    Catalog/
    Projects/

  FurniSpace.API.IntegrationTests/
    Fixtures/
      FurniSpaceWebApplicationFactory.cs
      IntegrationTestCollection.cs
    Authentication/
      TestAuthHandler.cs
      TestUser.cs
    Auth/
    Catalog/
    Projects/
    Quotations/
    Orders/
    Payments/
```

`FurniSpace.Testing` là shared library, không phải test runner. Fixtures, seed và fake dùng bởi nhiều integration-test project phải đặt tại đây để tránh copy giữa các suite.

Giữ các project `*.Tests` hiện tại cho unit/controller test. Không trộn test cần Docker vào các project này để developer vẫn có thể chạy unit test nhanh.

Các package cần thêm khi triển khai:

- `Microsoft.AspNetCore.Mvc.Testing`
- `Testcontainers.PostgreSql`
- `Testcontainers.Redis` khi bắt đầu auth/session suite
- Testcontainers module hoặc generic container cho Elasticsearch/MongoDB khi external suite đủ phạm vi
- `Respawn` hoặc cơ chế reset PostgreSQL tương đương
- `xunit`
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`

Khi triển khai, thêm package bằng `dotnet add package` để lấy phiên bản ổn định tương thích với .NET 8; không tự đoán version trong file project.

## 4. Các phase chuẩn bị và triển khai

Thứ tự phase tổng thể cần theo `implementation-roadmap-plan.md`:

```text
Phase 0: FurniSpace.Testing + trait/quy ước
Phase 1: Infrastructure integration với PostgreSQL thật
Phase 2: Application service integration
Phase 3 trở đi: API HTTP và external dependencies
```

Các phase chi tiết bên dưới tập trung vào track API HTTP sau khi foundation, migration fixture và seed dùng chung đã có.

### Phase 0 — Chốt phạm vi và tiêu chuẩn

Việc cần làm:

- Phân biệt repository integration, Application integration và API HTTP integration.
- Chốt dependency thật của từng suite: PostgreSQL cho core; Redis thật cho auth/session.
- Chốt provider bị fake: Gmail, Firebase, PayOS và các side effect realtime.
- Đánh dấu scenario cần Elasticsearch/MongoDB thành suite riêng.
- Chọn các workflow P0 trước khi viết hạ tầng test.

Đầu ra:

- Danh sách scenario P0/P1/P2.
- Quy ước đặt tên, trait và thời gian chạy tối đa.
- Quy tắc dữ liệu test và cleanup.

### Phase 1 — Làm API testable

Thay đổi tối thiểu:

1. Expose entry point ở cuối `Program.cs`:

```csharp
public partial class Program;
```

2. Tách startup database thành thành phần có thể cấu hình, ví dụ:

```text
StartupTasks:
  RunMigrations: true
  SeedMode: Demo | IntegrationTest | None
```

3. Với môi trường `IntegrationTest`:

- Migration failure phải throw và làm test fail.
- Không tự chạy `DataSeeder` demo.
- Fixture chịu trách nhiệm migrate và seed dữ liệu nhỏ, xác định.

4. Cho phép tắt Elasticsearch hosted initializer khi không chạy search suite.

5. Nếu việc override service trong `ConfigureTestServices` quá phức tạp, tách `AddInfrastructure` theo module:

```text
AddPostgresPersistence
AddRedisCaching
AddElasticsearchSearch
AddMongoRoomPlanner
AddExternalProviders
```

Điều kiện hoàn thành:

- `WebApplicationFactory<Program>` khởi động được với config chỉ do test cung cấp.
- Host core không kết nối Internet.
- Lỗi migration làm test đỏ, không bị bỏ qua.

### Phase 2 — Dựng test harness

`FurniSpaceWebApplicationFactory` cần:

- Start PostgreSQL container bằng dynamic port.
- Ghi đè connection string bằng in-memory configuration.
- Thiết lập environment là `IntegrationTest`.
- Gỡ/re-register các descriptor phụ thuộc PostgreSQL nếu production DI đã tạo `NpgsqlDataSource`.
- Thay Gmail, Firebase và PayOS bằng fake.
- Tắt Elasticsearch initializer và dùng no-op search implementation cho core suite.
- Thay realtime dispatcher bằng no-op khi scenario không kiểm tra SignalR.
- Tạo `HttpClient` với HTTPS redirect được xử lý rõ ràng.

Không đọc `.env` của developer để chạy test. `ApiIntegrationFixture` set process env vars (JWT, connection strings, Redis/ES placeholders) trước khi host start vì `Program` validate chúng sớm hơn `ConfigureAppConfiguration`. Factory in-memory config vẫn là backup; không cần `appsettings.IntegrationTest.json`.

Điều kiện hoàn thành:

- Một smoke test gọi endpoint `/` và nhận response thành công.
- Một request lỗi đi qua correlation-ID và exception middleware.
- Container được dispose dù test fail.

### Phase 3 — Chuẩn hóa database state

Quy trình đề xuất:

1. Start PostgreSQL một lần cho test collection.
2. Apply toàn bộ EF Core migrations.
3. Seed bộ dữ liệu nền tối thiểu: role và account cần thiết.
4. Trước mỗi test hoặc test class, reset bảng bằng Respawn.
5. Seed dữ liệu của scenario qua builder/factory.

Không dùng transaction rollback bao quanh toàn bộ HTTP test nếu application mở connection/transaction riêng; rollback có thể không bao phủ request thực.

Không dùng `DataSeeder` demo làm fixture chính. Nên tạo test-data builder với:

- ID có thể chỉ định.
- Email/code duy nhất.
- Timestamp cố định hoặc được điều khiển.
- Chỉ tạo dữ liệu mà scenario cần.

Các test liên quan expiry, token hoặc schedule nên dùng `TimeProvider` được inject thay cho gọi trực tiếp `DateTime.UtcNow` nếu cần tính xác định.

Điều kiện hoàn thành:

- Test chạy độc lập và chạy lại cho cùng kết quả.
- Thứ tự test không ảnh hưởng kết quả.
- Không truy cập database development.

### Phase 4 — Authentication và HTTP pipeline

Dùng hai chế độ:

**Test authentication**

- Custom authentication handler đọc test header hoặc helper.
- Sinh claims `NameIdentifier`, role và các claim cần thiết.
- Dùng cho đa số authorization/endpoint test để giảm coupling với login flow.

**Real authentication**

- Dành cho test register/login/refresh/logout/revocation.
- Dùng JWT setting cố định chỉ cho test.
- Dùng Redis Testcontainer thật khi triển khai auth/session suite ở phase sau.
- Không gửi email thật; fake email ghi nhận message/token ở memory để test có thể xác minh.

Scenario bắt buộc:

- Không có token trả `401`.
- Có token nhưng sai role trả `403`.
- Đúng role đi vào action.
- JWT thiếu claim bảo mật bị từ chối.
- Refresh token rotation và reuse/revocation.
- Logout đưa access token vào blacklist.
- Cookie và bearer-token behavior đúng như contract.

### Phase 5 — Viết workflow integration test theo ưu tiên

**P0 — chạy trên mọi pull request**

- API startup, migration và root/smoke endpoint.
- Auth: login, refresh, logout, `auth/me`.
- Validation và chuẩn response `ServiceResult<T>`.
- Catalog read: categories, products, product details.
- Project: create, get, role access và status transition quan trọng.
- Quotation -> accept -> order creation.
- Payment creation/status với provider fake.
- Các response `400`, `401`, `403`, `404`, `409` quan trọng.

**P1 — mở rộng core workflow**

- Project area và schedule.
- Proposal và customization workflow.
- Order adjustment/deposit/remaining payment.
- Notifications.
- Project chat text flow.
- File metadata flow với fake storage.
- Webhook signature, idempotency và duplicate event.

**P2 — dependency suite**

- Product/project/chat search với Elasticsearch thật.
- Room planner scene với MongoDB thật.
- SignalR handshake và authorization.
- Upload contract qua local fake HTTP/storage adapter.

Mỗi endpoint không cần mọi tổ hợp dữ liệu. Ưu tiên business workflow, authorization boundary, persistence side effect và failure path.

### Phase 6 — CI

Thêm integration-test job riêng vào `.github/workflows/ci.yml`:

```text
restore/build
  -> unit tests
  -> core integration tests
  -> optional external-dependency integration tests
```

Yêu cầu cho job:

- Runner có Docker.
- Testcontainers dùng dynamic port, không hard-code host port.
- Có timeout cho container startup và test run.
- Upload `.trx`, coverage và container logs khi fail.
- Không dùng secret production.
- Không chạy song song các test dùng chung database trừ khi mỗi test có database/schema riêng.
- Cache NuGet nhưng không cache database volume.

Lệnh mục tiêu:

```powershell
dotnet test tests\FurniSpace.API.IntegrationTests\FurniSpace.API.IntegrationTests.csproj -c Release
```

Có thể dùng trait để tách suite:

```powershell
dotnet test tests\FurniSpace.API.IntegrationTests\FurniSpace.API.IntegrationTests.csproj --filter "Category=Core"
dotnet test tests\FurniSpace.API.IntegrationTests\FurniSpace.API.IntegrationTests.csproj --filter "Category=ExternalDependency"
```

## 5. Danh sách thay đổi dự kiến trong codebase

### Production code

- `src/FurniSpace.API/Program.cs`
  - Thêm `public partial class Program`.
  - Cấu hình startup migration/seed theo environment/options.
  - Fail fast khi migration lỗi trong IntegrationTest.

- `src/FurniSpace.Infrastructure/DependencyInjection.cs`
  - Cho phép đăng ký dependency theo module hoặc bảo đảm service có thể override sạch trong test.
  - Cho phép tắt `ElasticsearchIndexInitializer`.

- `src/FurniSpace.Application/DependencyInjection.cs`
  - Nếu cần, tách registration để test không bắt buộc mọi external provider.

- `src/FurniSpace.Infrastructure/Data/DataSeeder.cs`
  - Giữ demo seed cho development.
  - Không dùng trực tiếp làm per-test seed.

- Các service dùng thời gian
  - Inject `TimeProvider` tại các luồng expiry/schedule dễ flaky.

### Test code mới

- `tests/FurniSpace.API.IntegrationTests/FurniSpace.API.IntegrationTests.csproj`
- Web application factory và xUnit collection fixture.
- PostgreSQL/Redis container fixtures.
- Database migration/reset helper.
- Test auth handler và user helper.
- Fake Gmail/Firebase/PayOS/search/realtime implementations.
- Test-data builders.
- Test theo module và workflow.

### Configuration và CI

- Fixture env overrides cho JWT/Postgres/Redis/ES placeholders (thay cho `appsettings.IntegrationTest.json`).
- `.github/workflows/ci.yml` thêm core integration-test job.
- Có thể thêm `docker-compose.integration.yml` nếu team chọn Compose thay Testcontainers, nhưng không nên duy trì cả hai cách làm cho cùng một suite.
- Cập nhật `FurniSpace.sln` để chứa project mới.

## 6. Yêu cầu để thực hiện test

### Máy phát triển

- .NET 8 SDK.
- Docker Desktop/Engine đang chạy Linux containers.
- Docker có đủ tài nguyên; nếu chạy Elasticsearch nên dành thêm khoảng 1–2 GB RAM.
- Quyền pull image PostgreSQL, Redis, MongoDB và Elasticsearch.
- Không cần Gmail, Firebase hoặc payment credential thật.

### Cấu hình bắt buộc do fixture cấp

- PostgreSQL connection string.
- Redis connection string.
- JWT issuer, audience và secret test dài tối thiểu 32 bytes.
- Cờ tắt demo seed.
- Cờ tắt Elasticsearch initializer trong core suite.
- Mongo/Elasticsearch endpoint chỉ trong dependency suite.
- Giá trị giả hợp lệ cho options được validate khi startup.

### Dữ liệu và bảo mật

- Chỉ dùng database/container tạm.
- Không dùng connection string development/staging/production.
- Không commit token, credential hoặc `.env` test có secret.
- Không log JWT, refresh token, OTP, webhook secret hoặc request body nhạy cảm.
- Fake provider cần lưu đủ metadata để assert nhưng không lưu secret thô không cần thiết.

### Tính ổn định

- Mọi network call phải có timeout.
- Test không dùng `Task.Delay` để chờ eventual consistency; dùng polling có deadline khi thật sự cần.
- Dữ liệu test phải unique hoặc reset trước test.
- Các test dùng shared fixture phải khai báo xUnit collection rõ ràng.
- Không dựa vào thứ tự chạy.
- Không assert timestamp chính xác theo thời gian máy; dùng khoảng hoặc `TimeProvider`.

## 7. Tiêu chí hoàn thành

Bộ integration test được xem là sẵn sàng khi:

- Fresh clone chạy được bằng một lệnh `dotnet test`.
- Không cần sửa `.env` cá nhân.
- Không gọi dịch vụ Internet thật.
- PostgreSQL migration được kiểm tra trên engine thật.
- Auth/role được kiểm tra qua HTTP pipeline.
- Mỗi test để database ở trạng thái có thể reset.
- Core suite ổn định trong CI và có thời gian chạy mục tiêu dưới 10 phút.
- Test fail cung cấp đủ log, correlation ID và artifact để điều tra.
- Có ít nhất một happy path và các failure path quan trọng cho mỗi workflow P0.

## 8. Thứ tự implementation khuyến nghị

1. Tạo shared project `FurniSpace.Testing`.
2. Dựng PostgreSQL Testcontainer, migration/reset helper và seed dùng chung.
3. Tạo `FurniSpace.Infrastructure.IntegrationTests` và hoàn thành repository pilot.
4. Tạo `FurniSpace.Application.IntegrationTests`, dùng PostgreSQL thật và fake external providers.
5. Thêm core integration job vào CI.
6. Tạo `FurniSpace.API.IntegrationTests`.
7. Expose `Program`, thêm startup options và dựng `WebApplicationFactory`.
8. Viết smoke test và test authentication/authorization bằng test authentication handler.
9. Viết project, quotation, order và payment workflow P0.
10. Sau khi core suite ổn định, dựng Redis Testcontainer cho real auth/session suite.
11. Chỉ khi Search/Mongo có đủ scenario, bổ sung external-dependency suite và job CI riêng.

## 9. Quyết định đã chốt

- Dùng **Testcontainers**, không duy trì Docker Compose riêng cho test runner. Dynamic port và lifecycle do fixture quản lý.
- Reset PostgreSQL bằng **Respawn trước mỗi test** để ưu tiên tính độc lập; chỉ tối ưu sau khi có số liệu thời gian chạy.
- **Redis triển khai sau**. Core HTTP suite hiện dùng test authentication và fake/no-op cho luồng không kiểm tra auth/session; Redis Testcontainer chỉ thêm khi bắt đầu real auth/refresh/logout suite.
- **Search/Mongo chưa cần làm sớm** vì phạm vi Mongo hiện còn mỏng. Khi đủ scenario, chạy thành external-dependency job riêng (ưu tiên nightly hoặc non-blocking); chỉ đưa vào required checks khi thời gian chạy và độ ổn định đạt yêu cầu.
