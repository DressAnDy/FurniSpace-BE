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

    [Fact]
    public async Task GetTrackingByOrderAsync_ReturnsProgressAndTimeline()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var repository = new DeliveryRepository(context);

        var tracking = await repository.GetTrackingByOrderAsync(data.OrderId, data.ProjectId);

        Assert.NotNull(tracking);
        Assert.Equal(OrderStatus.DELIVERING, tracking!.OrderStatus);
        Assert.Equal(10, tracking.TotalOrderedQuantity);
        Assert.Equal(3, tracking.TotalDeliveredQuantity);
        Assert.Equal(7, tracking.RemainingQuantity);
        Assert.Equal(30, tracking.DeliveryProgressPercent);
        Assert.Equal(1, tracking.CompletedDeliveryCount);
        Assert.Equal(1, tracking.UpcomingDeliveryCount);
        Assert.NotNull(tracking.NextDeliveryAt);
        Assert.Equal(2, tracking.Timeline.Count);
        Assert.Equal("Custom Chair", tracking.Items[0].ProductName);
        Assert.Equal(2, tracking.Timeline[0].Items.Count);
    }

    [Fact]
    public async Task GetTrackingByOrderAsync_WhenOrderProjectMismatch_ReturnsNull()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var repository = new DeliveryRepository(context);

        var tracking = await repository.GetTrackingByOrderAsync(data.OrderId, Guid.NewGuid());

        Assert.Null(tracking);
    }

    [Fact]
    public async Task GetProjectDeliverySummaryAsync_ReturnsAggregatedQuantities()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var repository = new DeliveryRepository(context);

        var summary = await repository.GetProjectDeliverySummaryAsync(data.ProjectId);

        Assert.NotNull(summary);
        Assert.Equal(ProjectStatus.DELIVERING, summary!.Status);
        Assert.Equal(10, summary.TotalQuantity);
        Assert.Equal(3, summary.DeliveredQuantity);
        Assert.Equal(7, summary.RemainingQuantity);
        Assert.Equal(30, summary.DeliveryProgressPercent);
        Assert.NotNull(summary.NextDeliveryAt);
    }

    [Fact]
    public async Task ExistsByProjectScheduleIdAsync_ReturnsTrueWhenLinked()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var repository = new DeliveryRepository(context);

        var exists = await repository.ExistsByProjectScheduleIdAsync(data.CompletedScheduleId);

        Assert.True(exists);
    }

    [Fact]
    public async Task GetByProjectScheduleIdAsync_ReturnsLinkedDelivery()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var repository = new DeliveryRepository(context);

        var delivery = await repository.GetByProjectScheduleIdAsync(data.CompletedScheduleId);

        Assert.NotNull(delivery);
        Assert.Equal(data.CompletedDeliveryId, delivery!.DeliveryId);
    }

    [Fact]
    public async Task HasInProgressDeliveryAsync_ReturnsTrueWhenBatchInProgress()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var hasInProgress = await repository.HasInProgressDeliveryAsync(data.OrderId);

        Assert.True(hasInProgress);
    }

    [Fact]
    public async Task GetProjectDeliverySummaryAsync_WhenProjectNotInDeliveryPhase_ReturnsNull()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        context.ProjectSet.Single(project => project.ProjectId == data.ProjectId).Status = ProjectStatus.PROPOSAL_CONSULTING;
        await context.SaveChangesAsync();
        var repository = new DeliveryRepository(context);

        var summary = await repository.GetProjectDeliverySummaryAsync(data.ProjectId);

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetTrackingByOrderAsync_IncludesAutoCancelledScheduleReason()
    {
        await using var context = CreateContext();
        var data = await SeedTrackingAsync(context);
        var cancelledScheduleId = Guid.NewGuid();
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = cancelledScheduleId,
            ProjectId = data.ProjectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Status = ProjectScheduleStatus.CANCELLED,
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            InternalNote = "ALL_ITEMS_ALREADY_DELIVERED",
            AssignedStaffId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        var repository = new DeliveryRepository(context);

        var tracking = await repository.GetTrackingByOrderAsync(data.OrderId, data.ProjectId);

        Assert.NotNull(tracking);
        var cancelledEntry = Assert.Single(tracking!.Timeline, entry => entry.ProjectScheduleId == cancelledScheduleId);
        Assert.Equal("ALL_ITEMS_ALREADY_DELIVERED", cancelledEntry.CancelReason);
    }

    [Fact]
    public async Task ExistsByProjectScheduleIdAsync_ReturnsFalseWhenNotLinked()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryRepository(context);

        var exists = await repository.ExistsByProjectScheduleIdAsync(Guid.NewGuid());

        Assert.False(exists);
    }

    [Fact]
    public async Task HasInProgressDeliveryAsync_ReturnsFalseWhenOnlyCompleted()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var inProgress = context.DeliverySet.Single(delivery => delivery.Status == DeliveryStatus.IN_PROGRESS);
        inProgress.Status = DeliveryStatus.COMPLETED;
        await context.SaveChangesAsync();
        var repository = new DeliveryRepository(context);

        var hasInProgress = await repository.HasInProgressDeliveryAsync(data.OrderId);

        Assert.False(hasInProgress);
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

    private static async Task<TrackingSeededData> SeedTrackingAsync(AppDbContext context)
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var quotationItemId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var completedScheduleId = Guid.NewGuid();
        var upcomingScheduleId = Guid.NewGuid();
        var completedDeliveryId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            Status = ProjectStatus.DELIVERING,
            ProjectCode = "PRJ-TRACK",
            ProjectName = "Tracking Project"
        });
        context.QuotationItemSet.Add(new QuotationItem
        {
            QuotationItemId = quotationItemId,
            QuotationId = quotationId,
            ItemName = "Custom Chair",
            Quantity = 10,
            UnitPrice = 50m,
            GrossAmount = 500m,
            DiscountAmount = 0m,
            TotalAmount = 500m
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "ORD-TRACK",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 40m,
            OriginalTotalAmount = 500m,
            FinalTotalAmount = 500m,
            Status = OrderStatus.DELIVERING
        });
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            QuotationItemId = quotationItemId,
            ProductVersionId = Guid.NewGuid(),
            ProductNameSnapshot = "Chair",
            Quantity = 10,
            DeliveredQuantity = 3,
            Status = OrderItemStatus.PARTIALLY_DELIVERED,
            UnitPrice = 50m,
            DiscountAmount = 0m,
            SubtotalAmount = 500m
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = completedScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.COMPLETED,
                ScheduledStart = now.AddDays(-2),
                ScheduledEnd = now.AddDays(-2).AddHours(2),
                CompletedAt = now.AddDays(-1),
                AssignedStaffId = Guid.NewGuid()
            },
            new ProjectSchedule
            {
                ScheduleId = upcomingScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                ScheduledStart = now.AddDays(2),
                ScheduledEnd = now.AddDays(2).AddHours(2),
                AssignedStaffId = Guid.NewGuid()
            });
        context.DeliverySet.Add(new Delivery
        {
            DeliveryId = completedDeliveryId,
            OrderId = orderId,
            ProjectScheduleId = completedScheduleId,
            Status = DeliveryStatus.COMPLETED,
            CreatedAt = now.AddDays(-2),
            CompletedAt = now.AddDays(-1)
        });
        context.DeliveryItemSet.AddRange(
            new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = completedDeliveryId,
                OrderItemId = orderItemId,
                Quantity = 2
            },
            new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = completedDeliveryId,
                OrderItemId = orderItemId,
                Quantity = 1
            });

        await context.SaveChangesAsync();
        return new TrackingSeededData(
            projectId,
            orderId,
            completedScheduleId,
            completedDeliveryId);
    }

    private sealed record SeededData(
        Guid OrderId,
        Guid OrderItemId,
        Guid OlderDeliveryId,
        Guid NewerDeliveryId);

    private sealed record TrackingSeededData(
        Guid ProjectId,
        Guid OrderId,
        Guid CompletedScheduleId,
        Guid CompletedDeliveryId);
}
