#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class OrderRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_ReturnsOrdersWithAssignmentMetadata()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var orders = await repository.GetByProjectAsync(data.ProjectId);

        Assert.Single(orders);
        Assert.Equal(data.OrderId, orders[0].OrderId);
        Assert.Equal(data.CustomerId, orders[0].CustomerId);
        Assert.Equal(data.SalesId, orders[0].AssignedSalesId);
        Assert.Equal(OrderStatus.DEPOSIT_PENDING, orders[0].Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsOrderWithItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var detail = await repository.GetDetailAsync(data.OrderId);

        Assert.NotNull(detail);
        Assert.Equal("ORD-001", detail!.OrderCode);
        Assert.Equal(data.SalesId, detail.AssignedSalesId);
        Assert.Single(detail.Items);
        Assert.Equal("Counter", detail.Items[0].ItemName);
        Assert.Equal(OrderItemStatus.READY, detail.Items[0].Status);
        Assert.Equal(1, detail.Items[0].DeliveredQuantity);
        Assert.Null(detail.Items[0].CustomerConfirmedAt);
    }

    [Fact]
    public async Task ExistsForQuotationAsync_WhenOrderExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var exists = await repository.ExistsForQuotationAsync(data.QuotationId);

        Assert.True(exists);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTrackedOrder()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var order = await repository.GetByIdAsync(data.OrderId);

        Assert.NotNull(order);
        Assert.Equal(data.OrderId, order!.OrderId);
    }

    [Fact]
    public async Task TryIncrementDeliveredQuantityAsync_WhenValidInMemory_UpdatesTrackedItem()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Quantity = 3;
        item.DeliveredQuantity = 1;
        item.Status = OrderItemStatus.READY;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);
        var deliveredBy = Guid.NewGuid();
        var deliveredAt = DateTime.UtcNow;

        var updated = await repository.TryIncrementDeliveredQuantityAsync(
            item.OrderItemId,
            2,
            "Loaded at front desk",
            deliveredBy,
            deliveredAt);

        Assert.NotNull(updated);
        Assert.Equal(3, updated!.DeliveredQuantity);
        Assert.Equal("Loaded at front desk", updated.DeliveryNote);
        Assert.Equal(deliveredBy, updated.LastDeliveredBy);
        Assert.Equal(deliveredAt, updated.LastDeliveredAt);
    }

    [Theory]
    [InlineData(OrderItemStatus.PENDING, 3, 1, 1)]
    [InlineData(OrderItemStatus.READY, 0, 0, 1)]
    [InlineData(OrderItemStatus.READY, 2, 2, 1)]
    public async Task TryIncrementDeliveredQuantityAsync_WhenInvalidInMemory_ReturnsNull(
        OrderItemStatus status,
        int quantity,
        int deliveredQuantity,
        int increment)
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Status = status;
        item.Quantity = quantity;
        item.DeliveredQuantity = deliveredQuantity;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var updated = await repository.TryIncrementDeliveredQuantityAsync(
            item.OrderItemId,
            increment,
            deliveryNote: null,
            deliveredBy: Guid.NewGuid(),
            deliveredAt: DateTime.UtcNow);

        Assert.Null(updated);
    }

    [Fact]
    public async Task TryIncrementDeliveredQuantityAsync_WhenItemMissingInMemory_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new OrderRepository(context);

        var updated = await repository.TryIncrementDeliveredQuantityAsync(
            Guid.NewGuid(),
            1,
            deliveryNote: null,
            deliveredBy: Guid.NewGuid(),
            deliveredAt: DateTime.UtcNow);

        Assert.Null(updated);
    }

    [Fact]
    public async Task TryIncrementDeliveredQuantityAsync_DefaultInterfaceImplementation_ReturnsNull()
    {
        IOrderRepository repository = new MinimalOrderRepository();

        var updated = await repository.TryIncrementDeliveredQuantityAsync(
            Guid.NewGuid(),
            1,
            deliveryNote: null,
            deliveredBy: Guid.NewGuid(),
            deliveredAt: DateTime.UtcNow);

        Assert.Null(updated);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var quotationItemId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com"),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com"));
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            ProjectCode = "PRJ-001",
            ProjectName = "Cafe",
            BusinessType = "Cafe",
            Status = ProjectStatus.ORDER_CONFIRMED,
            CreatedAt = DateTime.UtcNow
        });
        context.QuotationSet.Add(new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = Guid.NewGuid(),
            QuotationCode = "QT-001",
            SubtotalAmount = 100m,
            DiscountAmount = 0m,
            TaxableAmount = 100m,
            TaxAmount = 0m,
            TotalAmount = 100m,
            Currency = "VND",
            Status = QuotationStatus.ACCEPTED,
            CreatedAt = DateTime.UtcNow
        });
        context.QuotationItemSet.Add(new QuotationItem
        {
            QuotationItemId = quotationItemId,
            QuotationId = quotationId,
            ItemName = "Counter",
            ItemType = QuotationItemType.PRODUCT_ITEM,
            Quantity = 1,
            UnitPrice = 100m,
            CustomizationAdditionalCost = 0m,
            GrossAmount = 100m,
            DiscountAmount = 0m,
            TaxableAmount = 100m,
            TaxRate = 0m,
            TaxAmount = 0m,
            TotalAmount = 100m,
            SubtotalAmount = 100m
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "ORD-001",
            CustomerId = customerId,
            SalesId = salesId,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            CreatedAt = DateTime.UtcNow
        });
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            QuotationItemId = quotationItemId,
            ItemType = QuotationItemType.PRODUCT_ITEM,
            ProductNameSnapshot = "Counter",
            Quantity = 1,
            Status = OrderItemStatus.READY,
            DeliveredQuantity = 1,
            UnitPrice = 100m,
            CustomizationFee = 0m,
            GrossAmount = 100m,
            DiscountAmount = 0m,
            TaxableAmount = 100m,
            TaxRate = 0m,
            TaxAmount = 0m,
            TotalAmount = 100m,
            SubtotalAmount = 100m
        });

        await context.SaveChangesAsync();
        return new SeededData(projectId, orderId, quotationId, customerId, salesId);
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = "Test User",
            Phone = "0900000001",
            Status = AccountStatus.ACTIVE
        };
    }

    private sealed record SeededData(
        Guid ProjectId,
        Guid OrderId,
        Guid QuotationId,
        Guid CustomerId,
        Guid SalesId);

    private sealed class MinimalOrderRepository : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);

        public Task<OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<OrderDetailReadModel?>(null);

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<Order?>(null);

        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Order order)
        {
        }

        public IQueryable<Order> Query() => Enumerable.Empty<Order>().AsQueryable();

        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);

        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(Order entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
