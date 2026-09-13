using FurniSpace.Application.Common.Orders;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderItemStatusTransitionServiceTests
{
    [Theory]
    [InlineData(
        OrderItemStatus.PENDING,
        OrderItemStatus.IN_PRODUCTION,
        OrderItemStatusTransitionOwner.ProductionRequestCreation)]
    [InlineData(
        OrderItemStatus.IN_PRODUCTION,
        OrderItemStatus.READY,
        OrderItemStatusTransitionOwner.ProductionRequestCompletion)]
    [InlineData(
        OrderItemStatus.IN_PRODUCTION,
        OrderItemStatus.UNAVAILABLE,
        OrderItemStatusTransitionOwner.ProductionRequestCompletion)]
    [InlineData(
        OrderItemStatus.READY,
        OrderItemStatus.DELIVERED,
        OrderItemStatusTransitionOwner.CustomerDeliveryConfirmation)]
    [InlineData(
        OrderItemStatus.PHYSICALLY_DELIVERED,
        OrderItemStatus.DELIVERED,
        OrderItemStatusTransitionOwner.CustomerDeliveryConfirmation)]
    [InlineData(
        OrderItemStatus.PENDING,
        OrderItemStatus.CANCELLED,
        OrderItemStatusTransitionOwner.OrderCancellation)]
    [InlineData(
        OrderItemStatus.IN_PRODUCTION,
        OrderItemStatus.CANCELLED,
        OrderItemStatusTransitionOwner.OrderCancellation)]
    public void Validate_WhenTransitionAllowed_ReturnsNull(
        OrderItemStatus currentStatus,
        OrderItemStatus targetStatus,
        OrderItemStatusTransitionOwner owner)
    {
        var result = OrderItemStatusTransitionService.Validate(currentStatus, targetStatus, owner);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_WhenStatusPairInvalid_ReturnsInvalidTransition()
    {
        var result = OrderItemStatusTransitionService.Validate(
            OrderItemStatus.PENDING,
            OrderItemStatus.READY,
            OrderItemStatusTransitionOwner.ProductionRequestCompletion);

        Assert.NotNull(result);
        Assert.Equal(OrderItemStatusTransitionService.InvalidTransitionCode, result!.ErrorCode);
    }

    [Fact]
    public void Validate_WhenOwnerDoesNotOwnTransition_ReturnsOwnerMismatch()
    {
        var result = OrderItemStatusTransitionService.Validate(
            OrderItemStatus.READY,
            OrderItemStatus.DELIVERED,
            OrderItemStatusTransitionOwner.ProductionRequestCompletion);

        Assert.NotNull(result);
        Assert.Equal(OrderItemStatusTransitionService.OwnerMismatchCode, result!.ErrorCode);
    }

    [Fact]
    public void Validate_WhenCurrentStatusNull_ReturnsInvalidTransition()
    {
        var result = OrderItemStatusTransitionService.Validate(
            null,
            OrderItemStatus.DELIVERED,
            OrderItemStatusTransitionOwner.CustomerDeliveryConfirmation);

        Assert.NotNull(result);
        Assert.Equal(OrderItemStatusTransitionService.InvalidTransitionCode, result!.ErrorCode);
    }
}
