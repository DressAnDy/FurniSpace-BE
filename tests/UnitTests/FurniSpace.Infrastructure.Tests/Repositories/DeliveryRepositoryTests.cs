#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class DeliveryRepositoryTests
{
    [Fact]
    public async Task GetByOrderAsync_ReturnsDeliveriesOrderedByCreatedAtDesc()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var deliveries = await repository.GetByOrderAsync(data.OrderId);

        Assert.Equal(2, deliveries.Count);
        Assert.Equal(data.NewerDeliveryId, deliveries[0].DeliveryId);
        Assert.Equal(DeliveryStatus.IN_PROGRESS, deliveries[0].Status);
        Assert.Equal(1, deliveries[0].ItemCount);
        Assert.Equal(data.OlderDeliveryId, deliveries[1].DeliveryId);
        Assert.Equal(DeliveryStatus.COMPLETED, deliveries[1].Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsDeliveryWithItemSnapshots()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var detail = await repository.GetDetailAsync(data.OrderId, data.NewerDeliveryId);

        Assert.NotNull(detail);
        Assert.Equal(data.NewerDeliveryId, detail!.DeliveryId);
        Assert.Equal(DeliveryStatus.IN_PROGRESS, detail.Status);
        Assert.Single(detail.Items);
        Assert.Equal(data.OrderItemId, detail.Items[0].OrderItemId);
        Assert.Equal(2, detail.Items[0].Quantity);
        Assert.Equal("Oak Table", detail.Items[0].ProductNameSnapshot);
        Assert.Equal("Custom Oak Table", detail.Items[0].ItemName);
    }

    [Fact]
    public async Task GetDetailAsync_WhenOrderMismatch_ReturnsNull()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var detail = await repository.GetDetailAsync(Guid.NewGuid(), data.NewerDeliveryId);

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetItemsByDeliveryAsync_ReturnsOrderedItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var items = await repository.GetItemsByDeliveryAsync(data.NewerDeliveryId);

        Assert.Single(items);
        Assert.Equal(data.OrderItemId, items[0].OrderItemId);
    }

    [Fact]
    public async Task AddAsync_AndUpdate_PersistDelivery()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);
        var deliveryId = Guid.NewGuid();
        var delivery = new Delivery
        {
            DeliveryId = deliveryId,
            OrderId = data.OrderId,
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(delivery);
        await context.SaveChangesAsync();

        var tracked = await repository.GetByIdAsync(deliveryId);
        Assert.NotNull(tracked);

        tracked!.Status = DeliveryStatus.COMPLETED;
        tracked.CompletedAt = DateTime.UtcNow;
        repository.Update(tracked);
        await context.SaveChangesAsync();

        var updated = await context.DeliverySet.SingleAsync(entity => entity.DeliveryId == deliveryId);
        Assert.Equal(DeliveryStatus.COMPLETED, updated.Status);
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
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var quotationItemId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var olderDeliveryId = Guid.NewGuid();
        var newerDeliveryId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.QuotationItemSet.Add(new QuotationItem
        {
            QuotationItemId = quotationItemId,
            QuotationId = quotationId,
            ItemName = "Custom Oak Table",
            Quantity = 4,
            UnitPrice = 100m,
            GrossAmount = 400m,
            DiscountAmount = 0m,
            TotalAmount = 400m
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = Guid.NewGuid(),
            QuotationId = quotationId,
            OrderCode = "ORD-001",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 32m,
            OriginalTotalAmount = 400m,
            FinalTotalAmount = 400m
        });
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            QuotationItemId = quotationItemId,
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Oak Table",
            Quantity = 4,
            Status = OrderItemStatus.READY,
            UnitPrice = 100m,
            DiscountAmount = 0m,
            SubtotalAmount = 400m
        });
        context.DeliverySet.AddRange(
            new Delivery
            {
                DeliveryId = olderDeliveryId,
                OrderId = orderId,
                Status = DeliveryStatus.COMPLETED,
                CreatedAt = now.AddHours(-2)
            },
            new Delivery
            {
                DeliveryId = newerDeliveryId,
                OrderId = orderId,
                Status = DeliveryStatus.IN_PROGRESS,
                CreatedAt = now.AddHours(-1)
            });
        context.DeliveryItemSet.Add(new DeliveryItem
        {
            DeliveryItemId = Guid.NewGuid(),
            DeliveryId = newerDeliveryId,
            OrderItemId = orderItemId,
            Quantity = 2
        });

        await context.SaveChangesAsync();
        return new SeededData(orderId, orderItemId, olderDeliveryId, newerDeliveryId);
    }

    private sealed record SeededData(
        Guid OrderId,
        Guid OrderItemId,
        Guid OlderDeliveryId,
        Guid NewerDeliveryId);
}
