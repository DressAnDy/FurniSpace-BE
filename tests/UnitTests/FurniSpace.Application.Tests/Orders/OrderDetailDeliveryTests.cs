using System;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderDetailDeliveryComposerTests
{
    [Fact]
    public void BuildSummary_WhenTrackingMissing_ReturnsZeroSummary()
    {
        var summary = OrderDetailDeliveryComposer.BuildSummary(null);

        Assert.Equal(0, summary.TotalOrderedQuantity);
        Assert.Equal(0, summary.TotalDeliveredQuantity);
        Assert.Equal(0, summary.CompletedDeliveryCount);
        Assert.Equal(0, summary.InProgressDeliveryCount);
    }

    [Fact]
    public void BuildDeliveries_WhenNoDeliveries_ReturnsEmptyList()
    {
        var deliveries = OrderDetailDeliveryComposer.BuildDeliveries([], []);

        Assert.Empty(deliveries);
    }

    [Fact]
    public void BuildDeliveries_MapsBatchItemsAndScheduleFields()
    {
        var deliveryId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var scheduledStart = DateTime.UtcNow;

        var deliveries = OrderDetailDeliveryComposer.BuildDeliveries(
            [
                new DeliveryListItemReadModel
                {
                    DeliveryId = deliveryId,
                    OrderId = Guid.NewGuid(),
                    ProjectScheduleId = scheduleId,
                    Status = DeliveryStatus.COMPLETED,
                    CreatedAt = scheduledStart.AddHours(-1),
                    CompletedAt = scheduledStart.AddHours(1),
                    Schedule = new DeliveryScheduleSummaryReadModel
                    {
                        ProjectScheduleId = scheduleId,
                        ScheduledStart = scheduledStart,
                        ScheduledEnd = scheduledStart.AddHours(2),
                        Location = "Site A"
                    }
                }
            ],
            [
                new DeliveryItemReadModel
                {
                    DeliveryItemId = Guid.NewGuid(),
                    DeliveryId = deliveryId,
                    OrderItemId = orderItemId,
                    Quantity = 4,
                    ItemName = "Dining Chair"
                }
            ]);

        var batch = Assert.Single(deliveries);
        Assert.Equal(DeliveryStatus.COMPLETED, batch.Status);
        Assert.Equal(scheduleId, batch.ProjectScheduleId);
        Assert.Equal("Site A", batch.Location);
        var item = Assert.Single(batch.Items);
        Assert.Equal(orderItemId, item.OrderItemId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal("Dining Chair", item.ProductName);
    }

    [Fact]
    public void IsAwaitingCustomerConfirmation_WhenStatusMatches_ReturnsTrue()
    {
        Assert.True(OrderDetailDeliveryComposer.IsAwaitingCustomerConfirmation(
            OrderStatus.AWAITING_CUSTOMER_CONFIRMATION));
        Assert.False(OrderDetailDeliveryComposer.IsAwaitingCustomerConfirmation(
            OrderStatus.DELIVERING));
    }

    [Fact]
    public void BuildDeliveryDetails_MapsOrderFields()
    {
        var orderId = Guid.NewGuid();
        var details = OrderDetailDeliveryComposer.BuildDeliveryDetails(new OrderDetailReadModel
        {
            OrderId = orderId,
            DeliveryAddress = "123 ABC",
            ReceiverName = "A",
            ReceiverPhone = "09",
            DeliveryNote = "Note"
        });

        Assert.Equal(orderId, details.OrderId);
        Assert.Equal("123 ABC", details.DeliveryAddress);
        Assert.Equal("A", details.ReceiverName);
        Assert.Equal("09", details.ReceiverPhone);
        Assert.Equal("Note", details.DeliveryNote);
    }

    [Fact]
    public void BuildDeliveries_WhenBatchHasNoMappedItems_ReturnsEmptyItemList()
    {
        var deliveryId = Guid.NewGuid();

        var deliveries = OrderDetailDeliveryComposer.BuildDeliveries(
            [new DeliveryListItemReadModel { DeliveryId = deliveryId, OrderId = Guid.NewGuid() }],
            []);

        var batch = Assert.Single(deliveries);
        Assert.Empty(batch.Items);
    }

    [Fact]
    public void BuildDeliveries_UsesProductNameSnapshotWhenItemNameMissing()
    {
        var deliveryId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var deliveries = OrderDetailDeliveryComposer.BuildDeliveries(
            [new DeliveryListItemReadModel { DeliveryId = deliveryId, OrderId = Guid.NewGuid() }],
            [
                new DeliveryItemReadModel
                {
                    DeliveryItemId = Guid.NewGuid(),
                    DeliveryId = deliveryId,
                    OrderItemId = orderItemId,
                    Quantity = 1,
                    ProductNameSnapshot = "Snapshot Chair"
                }
            ]);

        var item = Assert.Single(Assert.Single(deliveries).Items);
        Assert.Equal("Snapshot Chair", item.ProductName);
    }

    [Fact]
    public void BuildSummary_WhenTrackingPresent_MapsProgressFields()
    {
        var summary = OrderDetailDeliveryComposer.BuildSummary(new OrderDeliveryTrackingReadModel
        {
            TotalOrderedQuantity = 10,
            TotalDeliveredQuantity = 4,
            RemainingQuantity = 6,
            DeliveryProgressPercent = 40,
            CompletedDeliveryCount = 1,
            InProgressDeliveryCount = 1,
            UpcomingDeliveryCount = 2,
            NextDeliveryAt = DateTime.UtcNow
        });

        Assert.Equal(10, summary.TotalOrderedQuantity);
        Assert.Equal(4, summary.TotalDeliveredQuantity);
        Assert.Equal(40, summary.DeliveryProgressPercent);
        Assert.Equal(1, summary.InProgressDeliveryCount);
    }
}

public sealed class OrderFinancialSummaryTests
{
    [Fact]
    public void FromItems_ComputesGrossDiscountAndPreVat()
    {
        var summary = OrderFinancialSummary.FromItems([
            new OrderItemDetailReadModel
            {
                Quantity = 2,
                UnitPrice = 100m,
                DiscountAmount = 10m
            },
            new OrderItemDetailReadModel
            {
                Quantity = 1,
                UnitPrice = 50m,
                DiscountAmount = 5m
            }
        ]);

        Assert.Equal(250m, summary.ItemsGrossAmount);
        Assert.Equal(15m, summary.TotalItemDiscountAmount);
        Assert.Equal(235m, summary.PreVatAmount);
    }
}
