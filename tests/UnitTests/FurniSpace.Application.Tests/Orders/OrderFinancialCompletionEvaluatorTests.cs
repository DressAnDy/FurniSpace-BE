#nullable enable

using System;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderFinancialCompletionEvaluatorTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void AreDeliverableItemsDelivered_WhenAllActiveItemsDelivered_ReturnsTrue()
    {
        var items = new[]
        {
            CreateDeliverableItem(OrderItemStatus.DELIVERED),
            CreateCancelledLineItem()
        };

        Assert.True(OrderFinancialCompletionEvaluator.AreDeliverableItemsDelivered(items));
    }

    [Fact]
    public void AreDeliverableItemsDelivered_WhenReadyItemExists_ReturnsFalse()
    {
        var items = new[] { CreateDeliverableItem(OrderItemStatus.READY) };

        Assert.False(OrderFinancialCompletionEvaluator.AreDeliverableItemsDelivered(items));
    }

    [Fact]
    public void AreDeliverableItemsDelivered_WhenNoActiveDeliveryItems_ReturnsTrue()
    {
        var items = new[] { CreateCancelledLineItem() };

        Assert.True(OrderFinancialCompletionEvaluator.AreDeliverableItemsDelivered(items));
    }

    [Fact]
    public void IsDeliveryConfirmedForFinancialCompletion_WhenConfirmedAndItemsDelivered_ReturnsTrue()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.True(OrderFinancialCompletionEvaluator.IsDeliveryConfirmedForFinancialCompletion(order, items));
    }

    [Fact]
    public void IsDeliveryConfirmedForFinancialCompletion_WhenNotConfirmed_ReturnsFalse()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: false);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.False(OrderFinancialCompletionEvaluator.IsDeliveryConfirmedForFinancialCompletion(order, items));
    }

    [Fact]
    public void IsDeliveryConfirmedForFinancialCompletion_WhenItemsNotDelivered_ReturnsFalse()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.READY) };

        Assert.False(OrderFinancialCompletionEvaluator.IsDeliveryConfirmedForFinancialCompletion(order, items));
    }

    [Fact]
    public void CanAutoCompleteAfterRemainingPayment_WhenOrderAlreadyCompleted_ReturnsFalse()
    {
        var order = CreateOrder(OrderStatus.COMPLETED, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.False(OrderFinancialCompletionEvaluator.CanAutoCompleteAfterRemainingPayment(order, items, 0m));
    }

    [Fact]
    public void AreDeliverableItemsDelivered_IgnoresUnavailableAndCancelledItems()
    {
        var items = new[]
        {
            new OrderItem
            {
                OrderId = OrderId,
                ProductVersionId = null,
                Quantity = 1,
                Status = OrderItemStatus.UNAVAILABLE
            },
            CreateCancelledLineItem()
        };

        Assert.True(OrderFinancialCompletionEvaluator.AreDeliverableItemsDelivered(items));
    }

    [Theory]
    [InlineData(OrderStatus.COMPLETED)]
    [InlineData(OrderStatus.DELIVERED)]
    public void CanAutoCompleteAfterRemainingPayment_WhenOrderStatusInvalid_ReturnsFalse(OrderStatus status)
    {
        var order = CreateOrder(status, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.False(OrderFinancialCompletionEvaluator.CanAutoCompleteAfterRemainingPayment(order, items, 0m));
    }

    [Fact]
    public void CanAutoCompleteAfterRemainingPayment_WhenRemainingAmountPositive_ReturnsFalse()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.False(OrderFinancialCompletionEvaluator.CanAutoCompleteAfterRemainingPayment(order, items, 1m));
    }

    [Fact]
    public void CanAutoCompleteAfterRemainingPayment_WhenDeliveryNotConfirmed_ReturnsFalse()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: false);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.False(OrderFinancialCompletionEvaluator.CanAutoCompleteAfterRemainingPayment(order, items, 0m));
    }

    [Fact]
    public void CanAutoCompleteAfterRemainingPayment_WhenAllGuardsPass_ReturnsTrue()
    {
        var order = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, confirmed: true);
        var items = new[] { CreateDeliverableItem(OrderItemStatus.DELIVERED) };

        Assert.True(OrderFinancialCompletionEvaluator.CanAutoCompleteAfterRemainingPayment(order, items, 0m));
    }

    private static Order CreateOrder(OrderStatus status, bool confirmed)
    {
        return new Order
        {
            OrderId = OrderId,
            FinalTotalAmount = 100m,
            Status = status,
            CustomerConfirmedDeliveryAt = confirmed ? DateTime.UtcNow : null
        };
    }

    private static OrderItem CreateDeliverableItem(OrderItemStatus status)
    {
        return new OrderItem
        {
            OrderId = OrderId,
            ProductVersionId = Guid.NewGuid(),
            Quantity = 1,
            Status = status
        };
    }

    private static OrderItem CreateCancelledLineItem()
    {
        return new OrderItem
        {
            OrderId = OrderId,
            ProductVersionId = Guid.NewGuid(),
            Quantity = 1,
            Status = OrderItemStatus.CANCELLED
        };
    }
}
