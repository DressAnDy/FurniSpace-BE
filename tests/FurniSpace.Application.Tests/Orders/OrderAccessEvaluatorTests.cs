using System;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderAccessEvaluatorTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();

    [Theory]
    [InlineData(OrderStatus.DEPOSIT_PAID, true)]
    [InlineData(OrderStatus.IN_PRODUCTION, true)]
    [InlineData(OrderStatus.COMPLETED, true)]
    [InlineData(OrderStatus.DEPOSIT_PENDING, false)]
    [InlineData(OrderStatus.CANCELLED, false)]
    public void CanViewOrder_ProductionRole_FiltersByOrderStatus(OrderStatus status, bool expected)
    {
        var result = OrderAccessEvaluator.CanViewOrder(
            OrderAccessEvaluator.ProductionRole,
            _customerId,
            _salesId,
            _designerId,
            _otherId,
            status);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanViewOrder_CustomerRole_AllowsOwnProjectOrders()
    {
        var result = OrderAccessEvaluator.CanViewOrder(
            "CUSTOMER",
            _customerId,
            _salesId,
            _designerId,
            _customerId,
            OrderStatus.DEPOSIT_PENDING);

        Assert.True(result);
    }

    [Fact]
    public void CanViewOrder_CustomerRole_DeniesOtherCustomerOrders()
    {
        var result = OrderAccessEvaluator.CanViewOrder(
            "CUSTOMER",
            _customerId,
            _salesId,
            _designerId,
            _otherId,
            OrderStatus.DEPOSIT_PENDING);

        Assert.False(result);
    }

    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("CUSTOMER", true)]
    [InlineData("SALES", true)]
    [InlineData("DESIGNER", false)]
    public void CanManageDepositPayment_ReturnsExpectedAccess(string role, bool expected)
    {
        var currentUserId = role switch
        {
            "CUSTOMER" => _customerId,
            "SALES" => _salesId,
            _ => _otherId
        };

        var result = OrderAccessEvaluator.CanManageDepositPayment(
            role,
            _customerId,
            _salesId,
            currentUserId);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("SALES", true)]
    [InlineData("CUSTOMER", false)]
    [InlineData("DESIGNER", false)]
    public void CanManageFinancialAdjustment_ReturnsExpectedAccess(string role, bool expected)
    {
        var currentUserId = role == "SALES" ? _salesId : _otherId;

        var result = OrderAccessEvaluator.CanManageFinancialAdjustment(
            role,
            _salesId,
            currentUserId);

        Assert.Equal(expected, result);
    }
}
