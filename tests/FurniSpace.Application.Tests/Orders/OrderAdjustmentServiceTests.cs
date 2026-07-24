#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderAdjustmentServiceTests
{
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public async Task CreateAdjustmentAsync_WhenOrderInProduction_CreatesDraftAdjustment()
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _salesId,
            new CreateOrderAdjustmentDto
            {
                Reason = " One item cannot be produced. ",
                InternalNote = " Production cancelled item. "
            });

        var adjustment = Assert.Single(context.OrderAdjustmentSet);
        Assert.Equal(201, result.Status);
        Assert.Equal("DRAFT", result.Data!.Status);
        Assert.Equal(0m, result.Data.TotalAdjustmentAmount);
        Assert.Equal("One item cannot be produced.", adjustment.Reason);
        Assert.Equal("Production cancelled item.", adjustment.InternalNote);
    }

    [Theory]
    [InlineData(OrderStatus.DEPOSIT_PAID, OrderErrorCodes.OrderNotInProduction)]
    [InlineData(OrderStatus.IN_PRODUCTION, OrderErrorCodes.InvalidAdjustment)]
    public async Task CreateAdjustmentAsync_WhenInvalid_ReturnsBadRequest(
        OrderStatus orderStatus,
        string expectedCode)
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, orderStatus);
        await context.SaveChangesAsync();
        var service = BuildService(context);
        var reason = expectedCode == OrderErrorCodes.InvalidAdjustment ? " " : "reason";

        var result = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _salesId,
            new CreateOrderAdjustmentDto { Reason = reason });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(context.OrderAdjustmentSet);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_WhenMissingUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            Guid.Empty,
            new CreateOrderAdjustmentDto { Reason = "reason" });
        var missing = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateOrderAdjustmentDto { Reason = "reason" });
        var forbidden = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _customerId,
            new CreateOrderAdjustmentDto { Reason = "reason" });

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, missing.ErrorCode);
        Assert.Equal(403, forbidden.Status);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenUnavailableItem_UsesOrderItemSubtotalAndRecalculates()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = seeded.OrderItemId,
                AdjustmentAmount = 2_000_000m,
                Reason = "Material unavailable."
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("UNAVAILABLE_ITEM", result.Data!.AdjustmentType);
        Assert.Equal(2_000_000m, result.Data.AdjustmentAmount);
        var adjustment = context.OrderAdjustmentSet.Single();
        Assert.Equal(2_000_000m, adjustment.ItemAdjustmentAmount);
        Assert.Equal(2_000_000m, adjustment.TotalAdjustmentAmount);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenAdditionalDiscount_RecalculatesDiscountTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 500_000m,
                Reason = "Compensation."
            });

        Assert.Equal(201, result.Status);
        Assert.Null(result.Data!.OrderItemId);
        Assert.Equal(500_000m, context.OrderAdjustmentSet.Single().AdditionalDiscountAmount);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenUnavailableInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var cancelled = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var wrongAmount = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = cancelled.OrderItemId,
                AdjustmentAmount = 1m,
                Reason = "wrong"
            });
        context.ProductionItemSet.Single().Status = ProductionItemStatus.IN_PRODUCTION;
        await context.SaveChangesAsync();
        var notCancelledResult = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = cancelled.OrderItemId,
                AdjustmentAmount = 2_000_000m,
                Reason = "not cancelled"
            });
        var missingOrderItem = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = Guid.NewGuid(),
                Reason = "missing"
            });

        Assert.Equal(OrderErrorCodes.InvalidUnavailableItemAmount, wrongAmount.ErrorCode);
        Assert.Equal(OrderErrorCodes.ProductionItemNotCancelled, notCancelledResult.ErrorCode);
        Assert.Equal(404, missingOrderItem.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotFound, missingOrderItem.ErrorCode);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenInvalidOrConfirmed_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var adjustment = await context.OrderAdjustmentSet.FindAsync(seeded.AdjustmentId);
        adjustment!.Status = OrderAdjustmentStatus.CONFIRMED;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var confirmed = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 500_000m,
                Reason = "confirmed"
            });
        adjustment.Status = OrderAdjustmentStatus.DRAFT;
        await context.SaveChangesAsync();
        var invalid = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 0m,
                Reason = "invalid"
            });
        var missingAdjustment = await service.AddAdjustmentItemAsync(
            Guid.NewGuid(),
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 1m,
                Reason = "missing"
            });

        Assert.Equal(OrderErrorCodes.AdjustmentAlreadyConfirmed, confirmed.ErrorCode);
        Assert.Equal(OrderErrorCodes.InvalidAdjustmentItem, invalid.ErrorCode);
        Assert.Equal(404, missingAdjustment.Status);
        Assert.Equal(OrderErrorCodes.OrderAdjustmentNotFound, missingAdjustment.ErrorCode);
    }

    [Fact]
    public async Task UpdateAdjustmentItemAsync_UpdatesItemAndRecalculatesTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        var item = CreateAdjustmentItem(seeded.AdjustmentId, OrderAdjustmentItemType.ADDITIONAL_DISCOUNT, 100_000m);
        context.OrderAdjustmentItemSet.Add(item);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateAdjustmentItemAsync(
            item.OrderAdjustmentItemId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 750_000m,
                Reason = "Updated compensation."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(750_000m, result.Data!.AdjustmentAmount);
        Assert.Equal("Updated compensation.", context.OrderAdjustmentItemSet.Single().Reason);
        Assert.Equal(750_000m, context.OrderAdjustmentSet.Single().AdditionalDiscountAmount);
    }

    [Fact]
    public async Task DeleteAdjustmentItemAsync_RemovesItemAndRecalculatesTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var item = CreateAdjustmentItem(seeded.AdjustmentId, OrderAdjustmentItemType.ADDITIONAL_DISCOUNT, 250_000m);
        context.OrderAdjustmentItemSet.Add(item);
        var adjustment = await context.OrderAdjustmentSet.FindAsync(seeded.AdjustmentId);
        adjustment!.AdditionalDiscountAmount = 250_000m;
        adjustment.TotalAdjustmentAmount = 250_000m;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.DeleteAdjustmentItemAsync(item.OrderAdjustmentItemId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Empty(context.OrderAdjustmentItemSet);
        Assert.Equal(0m, result.Data!.TotalAdjustmentAmount);
    }

    private OrderService BuildService(AppDbContext context)
    {
        return new OrderService(
            new OrderRepository(context),
            new ProjectRepository(context),
            new PaymentRepository(context),
            new InMemoryUnitOfWork(context));
    }

    private SeededOrder SeedOrderScenario(AppDbContext context, OrderStatus orderStatus)
    {
        var salesRole = CreateRole("SALES");
        var adminRole = CreateRole("ADMIN");
        var customerRole = CreateRole("CUSTOMER");
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.RoleSet.AddRange(salesRole, adminRole, customerRole);
        context.AccountSet.AddRange(
            CreateAccount(_salesId, salesRole.RoleId, "sales@example.com"),
            CreateAccount(Guid.NewGuid(), adminRole.RoleId, "admin@example.com"),
            CreateAccount(_customerId, customerRole.RoleId, "customer@example.com"));
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.IN_PRODUCTION
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = _customerId,
            SalesId = _salesId,
            OriginalTotalAmount = 5_000_000m,
            FinalTotalAmount = 5_000_000m,
            Status = orderStatus
        });
        return new SeededOrder(orderId, projectId);
    }

    private SeededAdjustmentItem SeedAdjustmentItemScenario(
        AppDbContext context,
        ProductionItemStatus productionItemStatus)
    {
        var order = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        var orderItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = order.OrderId,
            ItemType = QuotationItemType.PRODUCT_ITEM,
            ProductNameSnapshot = "Cabinet",
            Quantity = 1,
            SubtotalAmount = 2_000_000m,
            Status = OrderItemStatus.PENDING
        };
        var productionRequest = new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            Status = ProductionRequestStatus.IN_PRODUCTION
        };
        var productionItem = new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequest.ProductionRequestId,
            OrderItemId = orderItem.OrderItemId,
            Status = productionItemStatus
        };
        var adjustment = new OrderAdjustment
        {
            OrderAdjustmentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Status = OrderAdjustmentStatus.DRAFT,
            Reason = "Adjustment",
            CreatedBy = _salesId,
            CreatedAt = DateTime.UtcNow
        };
        context.OrderItemSet.Add(orderItem);
        context.ProductionRequestSet.Add(productionRequest);
        context.ProductionItemSet.Add(productionItem);
        context.OrderAdjustmentSet.Add(adjustment);
        return new SeededAdjustmentItem(adjustment.OrderAdjustmentId, orderItem.OrderItemId);
    }

    private OrderAdjustmentItem CreateAdjustmentItem(
        Guid adjustmentId,
        OrderAdjustmentItemType type,
        decimal amount)
    {
        return new OrderAdjustmentItem
        {
            OrderAdjustmentItemId = Guid.NewGuid(),
            OrderAdjustmentId = adjustmentId,
            AdjustmentType = type,
            AdjustmentAmount = amount,
            Reason = "Existing",
            CreatedBy = _salesId,
            CreatedAt = DateTime.UtcNow
        };
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

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = email,
            Status = AccountStatus.ACTIVE
        };
    }

    private sealed class InMemoryUnitOfWork(AppDbContext context) : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => context.SaveChangesAsync(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record SeededOrder(Guid OrderId, Guid ProjectId);

    private sealed record SeededAdjustmentItem(Guid AdjustmentId, Guid OrderItemId);
}
