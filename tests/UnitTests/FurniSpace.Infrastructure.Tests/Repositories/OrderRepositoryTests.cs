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
        Assert.NotNull(detail.Items[0].DeliveredAt);
        Assert.Equal(data.SalesId, detail.Items[0].DeliveredBy);
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
    public async Task AllDeliverableItemsReadyAsync_WhenAllReady_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var ready = await repository.AllDeliverableItemsReadyAsync(data.OrderId);

        Assert.True(ready);
    }

    [Fact]
    public async Task AllDeliverableItemsReadyAsync_WhenItemNotReady_ReturnsFalse()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Status = OrderItemStatus.PENDING;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var ready = await repository.AllDeliverableItemsReadyAsync(data.OrderId);

        Assert.False(ready);
    }

    [Fact]
    public async Task AllDeliverableItemsReadyAsync_WhenUnavailableItemExists_IgnoresUnavailableItem()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = data.OrderId,
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Unavailable Table",
            Quantity = 1,
            Status = OrderItemStatus.UNAVAILABLE,
            UnitPrice = 100m,
            DiscountAmount = 0m,
            SubtotalAmount = 100m
        });
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var ready = await repository.AllDeliverableItemsReadyAsync(data.OrderId);

        Assert.True(ready);
    }

    [Fact]
    public async Task AllDeliverableItemsDeliveredAsync_WhenAllDelivered_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Status = OrderItemStatus.DELIVERED;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var delivered = await repository.AllDeliverableItemsDeliveredAsync(data.OrderId);

        Assert.True(delivered);
    }

    [Fact]
    public async Task HasCompletedDeliveryFlowAsync_WhenOrderDelivered_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var order = await context.OrderSet.SingleAsync(order => order.OrderId == data.OrderId);
        order.Status = OrderStatus.DELIVERED;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var completed = await repository.HasCompletedDeliveryFlowAsync(data.ProjectId);

        Assert.True(completed);
    }

    [Fact]
    public async Task HasCompletedDeliveryFlowAsync_DefaultInterfaceImplementation_ReturnsFalse()
    {
        IOrderRepository repository = new MinimalOrderRepository();

        var completed = await repository.HasCompletedDeliveryFlowAsync(Guid.NewGuid());

        Assert.False(completed);
    }

    [Fact]
    public async Task AllDeliverableItemsDeliveredAsync_WhenItemStillReady_ReturnsFalse()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var delivered = await repository.AllDeliverableItemsDeliveredAsync(data.OrderId);

        Assert.False(delivered);
    }

    [Fact]
    public async Task HasCompletedDeliveryFlowAsync_WhenCustomerConfirmed_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var order = await context.OrderSet.SingleAsync(order => order.OrderId == data.OrderId);
        order.CustomerConfirmedDeliveryAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var completed = await repository.HasCompletedDeliveryFlowAsync(data.ProjectId);

        Assert.True(completed);
    }

    [Fact]
    public async Task HasCompletedDeliveryFlowAsync_WhenDeliverableItemDelivered_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Status = OrderItemStatus.DELIVERED;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var completed = await repository.HasCompletedDeliveryFlowAsync(data.ProjectId);

        Assert.True(completed);
    }

    [Fact]
    public async Task AllDeliverableItemsReadyAsync_DefaultInterfaceImplementation_ReturnsFalse()
    {
        IOrderRepository repository = new MinimalOrderRepository();

        var ready = await repository.AllDeliverableItemsReadyAsync(Guid.NewGuid());

        Assert.False(ready);
    }

    [Fact]
    public async Task AllDeliverableItemsDeliveredAsync_DefaultInterfaceImplementation_ReturnsFalse()
    {
        IOrderRepository repository = new MinimalOrderRepository();

        var delivered = await repository.AllDeliverableItemsDeliveredAsync(Guid.NewGuid());

        Assert.False(delivered);
    }

    [Fact]
    public async Task GetTotalRemainingDeliverableQuantityAsync_ReturnsRemainingQuantities()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Quantity = 5;
        item.DeliveredQuantity = 2;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var remaining = await repository.GetTotalRemainingDeliverableQuantityAsync(data.OrderId);

        Assert.Equal(3, remaining);
    }

    [Fact]
    public async Task HasProjectOrderInStatusesAsync_ReturnsTrueWhenStatusMatches()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OrderRepository(context);

        var hasOrder = await repository.HasProjectOrderInStatusesAsync(
            data.ProjectId,
            [OrderStatus.DEPOSIT_PENDING]);

        Assert.True(hasOrder);
    }

    [Fact]
    public async Task GetLatestByProjectInStatusesAsync_ReturnsMostRecentOrder()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var newerOrderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = newerOrderId,
            ProjectId = data.ProjectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-002",
            CustomerId = data.CustomerId,
            SalesId = data.SalesId,
            Status = OrderStatus.READY_FOR_DELIVERY,
            CreatedAt = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var latest = await repository.GetLatestByProjectInStatusesAsync(
            data.ProjectId,
            [OrderStatus.DEPOSIT_PENDING, OrderStatus.READY_FOR_DELIVERY]);

        Assert.NotNull(latest);
        Assert.Equal(newerOrderId, latest!.OrderId);
    }

    [Fact]
    public async Task GetItemsByIdsForUpdateAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var repository = new OrderRepository(context);

        var items = await repository.GetItemsByIdsForUpdateAsync([]);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetItemByIdAsync_ReturnsMatchingItem()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var orderItem = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        var repository = new OrderRepository(context);

        var item = await repository.GetItemByIdAsync(orderItem.OrderItemId);

        Assert.NotNull(item);
        Assert.Equal(orderItem.OrderItemId, item!.OrderItemId);
    }

    [Fact]
    public async Task AllDeliverableItemsDeliveredAsync_WhenQuantityFullyDelivered_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var item = await context.OrderItemSet.SingleAsync(item => item.OrderId == data.OrderId);
        item.Quantity = 4;
        item.DeliveredQuantity = 4;
        item.Status = OrderItemStatus.PARTIALLY_DELIVERED;
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var delivered = await repository.AllDeliverableItemsDeliveredAsync(data.OrderId);

        Assert.True(delivered);
    }

    [Fact]
    public async Task OrderRepositoryInterfaceDefaults_ReturnConfiguredFallbacks()
    {
        IOrderRepository repository = new MinimalOrderRepository();

        Assert.False(await repository.HasProjectOrderInStatusesAsync(Guid.NewGuid(), [OrderStatus.DELIVERING]));
        Assert.Null(await repository.GetLatestByProjectInStatusesAsync(Guid.NewGuid(), [OrderStatus.DELIVERING]));
        Assert.Equal(0, await repository.GetTotalRemainingDeliverableQuantityAsync(Guid.NewGuid()));
        Assert.Empty(await repository.GetItemsByIdsForUpdateAsync([Guid.NewGuid()]));
        Assert.Null(await repository.GetItemByIdAsync(Guid.NewGuid()));
        Assert.Empty(await repository.GetItemsByOrderAsync(Guid.NewGuid()));
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
            TotalDiscountAmount = 0m,
            PreVatAmount = 100m,
            VatRate = 0.08m,
            VatAmount = 8m,
            TotalAmount = 100m,
            DepositAmount = 30m,
            Currency = "VND",
            Status = QuotationStatus.ACCEPTED,
            CreatedAt = DateTime.UtcNow
        });
        context.QuotationItemSet.Add(new QuotationItem
        {
            QuotationItemId = quotationItemId,
            QuotationId = quotationId,
            ItemName = "Counter",
            Quantity = 1,
            UnitPrice = 100m,
            GrossAmount = 100m,
            DiscountAmount = 0m,
            TotalAmount = 100m
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "ORD-001",
            CustomerId = customerId,
            SalesId = salesId,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            CreatedAt = DateTime.UtcNow
        });
        var deliveredAt = DateTime.UtcNow;
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            QuotationItemId = quotationItemId,
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Counter",
            Quantity = 1,
            Status = OrderItemStatus.READY,
            DeliveredAt = deliveredAt,
            DeliveredBy = salesId,
            UnitPrice = 100m,
            DiscountAmount = 0m,
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
