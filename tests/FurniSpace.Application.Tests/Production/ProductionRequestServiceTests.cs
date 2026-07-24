#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Production;

public sealed class ProductionRequestServiceTests
{
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _productionId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesRequestItemsUpdatesWorkflowAndNotifies()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.OrderItemSet.AddRange(
            CreateOrderItem(data.OrderId, QuotationItemType.PRODUCT_ITEM, "Counter"),
            CreateOrderItem(data.OrderId, QuotationItemType.MANUAL_ITEM, "Shipping"));
        await context.SaveChangesAsync();
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(context, dispatcher);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto
            {
                AssignedTo = _productionId,
                Priority = " normal ",
                EstimatedStartDate = new DateOnly(2026, 7, 25),
                EstimatedCompletionDate = new DateOnly(2026, 8, 10),
                Note = " Start soon "
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(data.OrderId, result.Data!.OrderId);
        Assert.Equal(data.ProjectId, result.Data.ProjectId);
        Assert.Equal(_productionId, result.Data.AssignedTo);
        Assert.Equal("PENDING_REVIEW", result.Data.Status);
        Assert.Equal(1, result.Data.ProductionItemCount);
        Assert.StartsWith("PRD-", result.Data.ProductionCode, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.IN_PRODUCTION, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.IN_PRODUCTION, context.ProjectSet.Single().Status);
        Assert.Single(context.ProductionItemSet);
        Assert.Equal("NORMAL", context.ProductionRequestSet.Single().Priority);
        Assert.Equal("Start soon", context.ProductionRequestSet.Single().Note);
        Assert.Equal(NotificationType.ProductionRequestAssigned, dispatcher.NotificationType);
        Assert.Equal(_productionId, Assert.Single(dispatcher.ReceiverIds));
    }

    [Theory]
    [InlineData(OrderStatus.CREATED, PaymentStatus.PAID, ProductionErrorCodes.InvalidOrderStatus)]
    [InlineData(OrderStatus.DEPOSIT_PAID, PaymentStatus.PENDING, ProductionErrorCodes.DepositNotPaid)]
    public async Task CreateAsync_WhenOrderOrDepositInvalid_ReturnsBadRequest(
        OrderStatus orderStatus,
        PaymentStatus paymentStatus,
        string expectedCode)
    {
        await using var context = CreateContext();
        var data = SeedBase(context, orderStatus, paymentStatus);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
    }

    [Fact]
    public async Task CreateAsync_WhenActiveRequestExists_ReturnsConflict()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            AssignedTo = _productionId,
            Status = ProductionRequestStatus.PENDING_REVIEW
        });
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(409, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionRequestAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenProductionStaffInvalid_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = Guid.NewGuid() });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionStaffNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenOrderMissing_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenProjectMissing_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            SalesId = _salesId,
            OrderCode = "ORD-MISSING-PROJECT",
            Status = OrderStatus.DEPOSIT_PAID
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentCode = "PAY-MISSING-PROJECT",
            PaymentType = PaymentType.DEPOSIT,
            Status = PaymentStatus.PAID,
            Amount = 100m
        });
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            orderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenNotificationFails_DoesNotRollbackCreation()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.OrderItemSet.Add(CreateOrderItem(data.OrderId, QuotationItemType.PRODUCT_ITEM, "Chair"));
        await context.SaveChangesAsync();
        var service = BuildService(context, new FailingNotificationDispatcher());

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(201, result.Status);
        Assert.Single(context.ProductionRequestSet);
        Assert.Single(context.ProductionItemSet);
    }

    [Fact]
    public async Task CreateAsync_WhenDateRangeInvalid_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateProductionRequestDto
            {
                AssignedTo = _productionId,
                EstimatedStartDate = new DateOnly(2026, 8, 10),
                EstimatedCompletionDate = new DateOnly(2026, 7, 25)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.InvalidProductionRequestDate, result.ErrorCode);
    }

    [Fact]
    public async Task GetAvailableStaffAsync_ReturnsActiveProductionStaffWithWorkload()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.ProductionRequestSet.AddRange(
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.PENDING_REVIEW),
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.IN_PRODUCTION),
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.COMPLETED));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { Search = "production" });

        Assert.Equal(200, result.Status);
        var staff = Assert.Single(result.Data!);
        Assert.Equal(_productionId, staff.AccountId);
        Assert.Equal("ACTIVE", staff.AccountStatus);
        Assert.Equal(2, staff.ActiveRequestCount);
        Assert.Equal(1, staff.PendingReviewRequestCount);
        Assert.Equal(1, staff.InProductionRequestCount);
        Assert.Equal(0, staff.BlockedRequestCount);
        Assert.True(staff.IsAvailable);
    }

    [Fact]
    public async Task GetAvailableStaffAsync_ValidatesFilters()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var tooLong = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { Search = new string('x', 151) });
        var missingProject = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { ProjectId = Guid.NewGuid() });
        var missingRequest = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { ProductionRequestId = Guid.NewGuid() });

        Assert.Equal(ProductionErrorCodes.InvalidProductionStaffFilter, tooLong.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProjectNotFound, missingProject.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProductionRequestNotFound, missingRequest.ErrorCode);
    }

    [Fact]
    public async Task Methods_WhenUserUnauthorizedOrForbidden_ReturnExpectedStatus()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.GetAvailableStaffAsync(Guid.Empty, new AvailableProductionStaffQueryDto());
        var forbidden = await service.GetAvailableStaffAsync(_productionId, new AvailableProductionStaffQueryDto());

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
    }

    private ProductionRequestService BuildService(
        AppDbContext context,
        INotificationDispatcher? dispatcher = null)
    {
        return new ProductionRequestService(
            new ProductionRequestRepository(context),
            new OrderRepository(context),
            new ProjectRepository(context),
            new PaymentRepository(context),
            new ProductionRequestServiceDependencies(
                new InMemoryUnitOfWork(context),
                dispatcher,
                logger: null));
    }

    private SeededData SeedBase(AppDbContext context, OrderStatus orderStatus, PaymentStatus paymentStatus)
    {
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = _salesId,
            ProjectName = "Cafe Project",
            Status = ProjectStatus.ORDER_CONFIRMED
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = orderStatus
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            PaymentCode = "PAY-001",
            PaymentType = PaymentType.DEPOSIT,
            Status = paymentStatus,
            Amount = 100m
        });
        return new SeededData(projectId, orderId);
    }

    private void SeedRolesAndAccounts(AppDbContext context, AccountStatus productionStatus)
    {
        var salesRole = CreateRole("SALES");
        var productionRole = CreateRole("PRODUCTION");
        context.RoleSet.AddRange(salesRole, productionRole, CreateRole("CUSTOMER"));
        context.AccountSet.AddRange(
            CreateAccount(_salesId, salesRole.RoleId, "sales@example.com", "Sales User", AccountStatus.ACTIVE),
            CreateAccount(_productionId, productionRole.RoleId, "production@example.com", "Production User", productionStatus));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Role CreateRole(string roleName)
    {
        return new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = roleName,
            Description = $"{roleName} role"
        };
    }

    private static Account CreateAccount(
        Guid accountId,
        Guid roleId,
        string email,
        string fullName,
        AccountStatus status)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = fullName,
            Status = status
        };
    }

    private static OrderItem CreateOrderItem(
        Guid orderId,
        QuotationItemType itemType,
        string productName)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ItemType = itemType,
            ProductVersionId = itemType == QuotationItemType.PRODUCT_ITEM ? Guid.NewGuid() : null,
            ProductNameSnapshot = productName,
            ProductVersionNameSnapshot = $"{productName} Version",
            Quantity = 2,
            ProductionNote = "Use premium finish"
        };
    }

    private static ProductionRequest CreateProductionRequest(
        Guid projectId,
        Guid orderId,
        Guid assignedTo,
        ProductionRequestStatus status)
    {
        return new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            AssignedTo = assignedTo,
            Status = status
        };
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public InMemoryUnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? NotificationType { get; private set; }
        public List<Guid> ReceiverIds { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            NotificationType = type;
            ReceiverIds.AddRange(receiverIds);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Notification failed.");
        }
    }

    private sealed record SeededData(Guid ProjectId, Guid OrderId);
}
