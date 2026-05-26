# FurniSpace Backend API Developer Guide

This document summarizes the `src/` backend structure and provides a practical API implementation workflow for backend developers. The repository is currently a .NET 8 Web API following a Clean Architecture direction, but many classes are still empty scaffolds. When adding a new API, developers need to implement the feature and wire the required pipeline pieces at the same time.

## 1. Architecture Overview

The backend lives under `src/` and contains 5 projects:

| Project | Responsibility | Should contain |
| --- | --- | --- |
| `FurniSpace.API` | HTTP entrypoint layer | `Program.cs`, controllers, middleware, filters, Swagger extensions |
| `FurniSpace.Application` | Use case layer | Commands, queries, handlers, DTOs, validation, service interfaces |
| `FurniSpace.Domain` | Core business layer | Entities, value objects, domain events, specifications, domain exceptions |
| `FurniSpace.Infrastructure` | Infrastructure and persistence layer | EF Core `AppDbContext`, repositories, Unit of Work, auth, cache, logging |
| `FurniSpace.Shared` | Shared utilities | Constants, extensions, environment loader, date/time helpers |

Current dependency direction:

```text
API -> Application
API -> Infrastructure
API -> Shared
Application -> Domain
Infrastructure -> Domain
Shared -> independent
```

Rules when adding APIs:

- `Domain` must not depend on any other project.
- `Application` declares use cases and required interfaces, but should not call EF Core directly.
- `Infrastructure` implements database, cache, auth, email, repository, and external service details.
- `API` only handles HTTP concerns: routes, auth policies, status codes, and request/response mapping.

## 2. Current Important Status

Already available:

- Serilog logging in `FurniSpace.API/Program.cs`.
- Docker Compose services for `api`, `postgres`, and `redis`.
- Sample entities: `User`, `RefreshToken`, `Role`.
- Sample value objects: `Email`, `Address`, `Money`.
- Sample specification: `ActiveUserByEmailSpec`.
- Command/query folder structure for `Users`.
- `BaseApiController` already has `[ApiController]` and route `api/[controller]`.

Items that must be completed before the API can run fully:

- `Program.cs` currently only maps `/`; it does not call `AddControllers()` or `MapControllers()`.
- `DependencyInjection` classes in Application and Infrastructure are empty.
- `UsersController` and `AuthController` are empty.
- Application commands, queries, handlers, and validators are empty.
- Repository, UnitOfWork, Cache, AuthService, and JwtTokenService classes are empty.
- `AppDbContext` does not have `DbSet` properties and does not apply entity configurations.
- `UserConfiguration` currently configures `object`; it should be changed to `IEntityTypeConfiguration<User>`.
- `UserMappingConfig` exists but is empty. The recommended mapper for this project is Mapster.
- MediatR, FluentValidation, Swagger, and JWT packages are not yet added to `.csproj` files if the project will use the full CQRS + validation + OpenAPI pattern.

Because of this, when implementing a new feature, developers should follow the full workflow in section 5 instead of only creating a controller.

## 3. Folder Convention for a New API Module

Example structure for a `Products` module:

```text
src/
  FurniSpace.Domain/
    Entities/Product.cs
    ValueObjects/...
    Specifications/ProductBySkuSpec.cs

  FurniSpace.Application/
    DTOs/ProductDto.cs
    Features/Products/
      Commands/CreateProduct/
        CreateProductCommand.cs
        CreateProductHandler.cs
        CreateProductValidator.cs
      Commands/UpdateProduct/
      Queries/GetProductById/
      Queries/GetProductsPaged/

  FurniSpace.Infrastructure/
    Persistence/Configurations/ProductConfiguration.cs
    Repositories/IRepository/IProductRepository.cs
    Repositories/Repository/ProductRepository.cs

  FurniSpace.API/
    Controllers/ProductsController.cs
```

Routes should use plural resource names:

```text
GET    /api/products
GET    /api/products/{id}
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}
```

## 4. Expected Request Flow

The processing flow should follow this chain:

```text
HTTP Request
  -> Controller
  -> Command/Query
  -> Handler
  -> Domain Entity/Value Object
  -> Repository/UnitOfWork
  -> AppDbContext/PostgreSQL
  -> DTO/ServiceResult
  -> HTTP Response
```

Controllers should not contain business logic. Business logic should live in:

- Entity/value object classes for core domain rules.
- Handlers for use case workflows.
- Infrastructure services for JWT, email, cache, storage, or external API concerns.

## 5. Steps to Implement a New API

### Step 1: Define the Use Case and Contract

Write the endpoint contract first:

```text
POST /api/products
Input: name, sku, price, dimensions, material
Output: id, name, sku, price, createdAt
Status: 201, 400, 409, 500
```

Clarify:

- Who is allowed to call this API?
- Which fields are required?
- Which business errors can happen?
- Does the API need a transaction?
- Does the response need paging, filtering, or sorting?

### Step 2: Add the Domain Model

Create the entity in `FurniSpace.Domain/Entities`.

Entities should hide setters and expose meaningful methods:

```csharp
public sealed class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private Product() { }

    public static Product Create(string name, string sku, Money price)
    {
        return new Product
        {
            Name = name.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Price = price,
            IsActive = true
        };
    }

    public void ChangePrice(Money price)
    {
        Price = price;
        SetUpdatedAt();
    }
}
```

If a field has its own rules, prefer a value object. For example, `Email.Create(...)` currently returns `Result<Email>` instead of throwing an exception.

### Step 3: Add DTOs and Commands/Queries

Place DTOs in `FurniSpace.Application/DTOs`.

Place commands and queries under the feature folder:

```text
Features/Products/Commands/CreateProduct/CreateProductCommand.cs
Features/Products/Queries/GetProductById/GetProductByIdQuery.cs
```

Using records is recommended because request contracts become clear:

```csharp
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    decimal PriceAmount,
    string Currency);
```

If MediatR is installed, commands and queries should implement:

```csharp
IRequest<ServiceResult<ProductDto>>
```

### Step 3.1: Add Mapping with Mapster

FurniSpace should use Mapster as the default mapper. It is lightweight, simple to configure, and fits the current Clean Architecture structure without adding much ceremony.

Install Mapster in the Application project:

```powershell
dotnet add src/FurniSpace.Application package Mapster
```

If API or Infrastructure also needs to call `.Adapt<T>()` directly, install Mapster in that project too. Prefer keeping mapping in Application handlers whenever possible.

Create one mapping config per module in `FurniSpace.Application/Mappings`.

Example:

```csharp
using FurniSpace.Application.DTOs;
using FurniSpace.Domain.Entities;
using Mapster;

namespace FurniSpace.Application.Mappings;

public static class ProductMappingConfig
{
    public static void Register()
    {
        TypeAdapterConfig<Product, ProductDto>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Sku, src => src.Sku)
            .Map(dest => dest.PriceAmount, src => src.Price.Amount)
            .Map(dest => dest.Currency, src => src.Price.Currency)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt);
    }
}
```

Register all mapping configs from `AddApplication`:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    UserMappingConfig.Register();
    ProductMappingConfig.Register();

    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    return services;
}
```

Use Mapster in handlers:

```csharp
using Mapster;

var dto = product.Adapt<ProductDto>();
return ServiceResult<ProductDto>.Success(dto);
```

Mapping rules:

- Keep Mapster configuration in `FurniSpace.Application/Mappings`.
- Do not put mapping configuration in controllers.
- Use explicit config for value objects, nested objects, computed fields, and renamed properties.
- For very simple mappings with identical property names, `.Adapt<T>()` can be used without extra config.
- Do not expose domain entities directly from API responses; always return DTOs.

### Step 4: Add Validation

Place validators in the same folder as the related command/query:

```csharp
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.PriceAmount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
```

If FluentValidation is not used yet, there are 2 options:

- Add the package and implement `ValidationBehavior<TRequest,TResponse>`.
- Validate manually in the handler/controller as a temporary step.

Recommendation: use FluentValidation so validation stays centralized in the Application layer.

### Step 5: Add Repository and Unit of Work

Repository interfaces should live in `Infrastructure/Repositories/IRepository`.

```csharp
public interface IProductRepository : IGenericRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
```

Implementations should live in `Infrastructure/Repositories/Repository`.

```csharp
public sealed class ProductRepository : GenericRepository<Product>, IProductRepository
{
    private readonly AppDbContext _dbContext;

    public ProductRepository(AppDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Product>()
            .FirstOrDefaultAsync(x => x.Sku == sku && !x.IsDeleted, cancellationToken);
    }
}
```

`IGenericRepository<T>` is currently empty, so it should include basic methods:

```csharp
Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
Task AddAsync(T entity, CancellationToken cancellationToken = default);
void Update(T entity);
void Remove(T entity);
```

`IUnitOfWork` should include:

```csharp
Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
```

### Step 6: Configure EF Core

Add a `DbSet` to `AppDbContext`:

```csharp
public DbSet<Product> Products => Set<Product>();
```

Override model creation:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

Create the entity configuration:

```csharp
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.OwnsOne(x => x.Price);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

Note: `UserConfiguration` is currently `IEntityTypeConfiguration<object>`. It should be changed to `IEntityTypeConfiguration<User>` when implementing the User API.

### Step 7: Implement the Handler

The handler coordinates the use case:

```csharp
using Mapster;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, ServiceResult<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductHandler(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var existing = await _products.GetBySkuAsync(request.Sku, cancellationToken);
        if (existing is not null)
        {
            return ServiceResult<ProductDto>.Failure(Error.Conflict("Product.SkuExists", "SKU already exists"));
        }

        var money = Money.Create(request.PriceAmount, request.Currency);
        if (!money.IsSuccess)
        {
            return ServiceResult<ProductDto>.BadRequest(money.Error!.Message);
        }

        var product = Product.Create(request.Name, request.Sku, money.Value!);
        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDto>.Created(product.Adapt<ProductDto>());
    }
}
```

The Application layer provides `ServiceResult<T>`, `Error`, and `PagedResult<T>` models under `Common/Results`. Use them consistently:

- Use `ServiceResult<T>` for handler output and API response envelopes.
- Use `PagedResult<T>` as the data payload for paged endpoints.
- Domain value objects may keep using `Domain.Common.Result<T>`.
- Map Domain validation failures to `ServiceResult<T>.BadRequest(...)`, or convert them to an Application `Error`.

### Step 8: Register Dependency Injection

In `FurniSpace.Application/DependencyInjection.cs`:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    UserMappingConfig.Register();
    ProductMappingConfig.Register();

    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    return services;
}
```

In `FurniSpace.Infrastructure/DependencyInjection.cs`:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<ICacheService, RedisCacheService>();

    return services;
}
```

Add the required NuGet packages to the relevant projects if they are not installed yet.

### Step 9: Wire the Pipeline in Program.cs

`Program.cs` does not map controllers yet. When implementing real APIs, it should have at least:

```csharp
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapControllers();
app.MapGet("/", () => "FurniSpace API");
app.Run();
```

If authentication is added:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Recommended middleware order:

```text
Exception handling
Correlation id
Request logging
Authentication
Authorization
Controllers
```

### Step 10: Create the Controller

The controller should only receive the request and call Mediator/service:

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

        if (result.Status >= 400)
        {
            return StatusCode(result.Status, result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return StatusCode(result.Status, result);
    }
}
```

Later, add a helper in `BaseApiController` to map `IServiceResult` to `IActionResult`, so every controller does not repeat status-code handling.

### Step 11: Add Migration and Update the Database

After EF configuration is stable:

```powershell
dotnet ef migrations add AddProducts `
  --project src/FurniSpace.Infrastructure `
  --startup-project src/FurniSpace.API

dotnet ef database update `
  --project src/FurniSpace.Infrastructure `
  --startup-project src/FurniSpace.API
```

If EF CLI is not installed:

```powershell
dotnet tool install --global dotnet-ef
```

### Step 12: Test the API

Minimum test checklist:

- Build the solution: `dotnet build FurniSpace.sln`
- Run the local API: `dotnet run --project src/FurniSpace.API`
- Open Swagger if enabled.
- Test the happy path.
- Test validation errors.
- Test conflict/duplicate cases.
- Test not found cases.
- Test auth/authorization if the endpoint requires a role.
- Test database transactions if multiple write operations are involved.

## 6. Checklist for Adding an Endpoint

Use this checklist for every API:

- [ ] Define route, request, response, and status codes.
- [ ] Create/update entity or value object in Domain.
- [ ] Create DTO in Application.
- [ ] Create/update Mapster mapping config if properties are renamed, nested, computed, or value-object based.
- [ ] Create command/query.
- [ ] Create validator.
- [ ] Create handler.
- [ ] Create the required repository method.
- [ ] Create/update EF configuration.
- [ ] Add `DbSet` if this is a new entity.
- [ ] Register DI for repository/service.
- [ ] Create controller action.
- [ ] Add migration if the database schema changes.
- [ ] Build the solution.
- [ ] Test the endpoint using Swagger/Postman/curl.
- [ ] Update API docs if the response contract changes.

## 7. Recommended Backend Completion Order

Starting from the current repository state, the recommended order is:

1. Complete the API pipeline:
   - Add `AddControllers` and `MapControllers`.
   - Add Swagger.
   - Enable existing middleware once their implementation is ready.

2. Complete Application infrastructure:
   - Choose MediatR + FluentValidation or a service-based pattern.
   - Add Mapster and register mapping configs.
   - Implement `ServiceResult<T>`, `Error`, and `PagedResult<T>`.
   - Implement `ValidationBehavior`.

3. Complete EF Core setup:
   - Add connection string.
   - Register `AppDbContext`.
   - Add `DbSet<User>`.
   - Fix `UserConfiguration`.
   - Add the first migration.

4. Complete repository/unit of work:
   - `IGenericRepository<T>`.
   - `GenericRepository<T>`.
   - `IUnitOfWork`.
   - `UnitOfWork`.
   - `IUserRepository` and `UserRepository`.

5. Complete Auth/User API:
   - Register/login/refresh token.
   - Create user.
   - Get user by id.
   - Get users paged.
   - Update user.

6. Add business modules:
   - Project management.
   - Furniture catalog.
   - 3D design data.
   - Quotation/order.
   - Production/delivery.

## 8. Suggested APIs for the FurniSpace Domain

Priority API groups:

| Module | Sample endpoints |
| --- | --- |
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh-token` |
| Users | `GET /api/users`, `GET /api/users/{id}`, `PUT /api/users/{id}` |
| Projects | `POST /api/projects`, `GET /api/projects/{id}`, `PUT /api/projects/{id}/dimensions` |
| Design | `POST /api/projects/{id}/designs`, `PUT /api/designs/{id}/scene`, `GET /api/designs/{id}` |
| Furniture | `GET /api/furniture`, `GET /api/furniture/{id}`, `POST /api/furniture` |
| Quotes | `POST /api/projects/{id}/quotes`, `GET /api/quotes/{id}`, `POST /api/quotes/{id}/approve` |
| Orders | `POST /api/orders`, `GET /api/orders/{id}`, `PUT /api/orders/{id}/status` |
| Production | `GET /api/production-jobs`, `PUT /api/production-jobs/{id}/status` |
| Delivery | `GET /api/deliveries`, `PUT /api/deliveries/{id}/status` |

## 9. Response and Error Conventions

Use a consistent success response:

```json
{
  "id": "6fa459ea-ee8a-3ca4-894e-db77e160355e",
  "name": "Display Shelf",
  "createdAt": "2026-05-22T10:00:00Z"
}
```

Paging response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Error response:

```json
{
  "code": "Product.SkuExists",
  "message": "SKU already exists",
  "traceId": "..."
}
```

Recommended status code mapping:

| Error type | HTTP |
| --- | --- |
| Validation | `400 Bad Request` |
| Authentication | `401 Unauthorized` |
| Authorization | `403 Forbidden` |
| Not found | `404 Not Found` |
| Conflict/duplicate | `409 Conflict` |
| Unexpected exception | `500 Internal Server Error` |

## 10. Repository-Specific Notes

- `bin/` and `obj/` currently exist under `src/`; ignore them when scanning code.
- `.env` is loaded through `EnvLoader.LoadEnv(required: false)`.
- Docker exposes the API through host port `5000` to container port `8080`.
- PostgreSQL in Docker exposes host port `5433` to container port `5432`.
- Redis uses a password from `.env`.
- Update `.gitignore` or repository hygiene if `bin/obj` were already tracked.
- If new packages are added, rebuild the solution to confirm all project references are correct.

## 11. Quick Template for a New API

Copy this checklist into an issue/task:

```text
Feature:
Route:
Request:
Response:
Permission:

Domain:
- Entity/value object:
- Business rules:

Application:
- DTO:
- Mapster mapping:
- Command/query:
- Validator:
- Handler:

Infrastructure:
- Repository:
- EF configuration:
- Migration:

API:
- Controller/action:
- Status code mapping:

Tests:
- Happy path:
- Validation:
- Not found/conflict:
- Auth:
```
