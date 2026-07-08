using FurniSpace.Application.Common.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderCalculatorTests
{
    [Theory]
    [InlineData(1000, 30, 300)]
    [InlineData(250, 30, 75)]
    [InlineData(100, 50, 50)]
    [InlineData(0, 30, 0)]
    [InlineData(-10, 30, 0)]
    public void OrderDepositCalculator_ReturnsTruncatedDeposit(decimal total, int percent, decimal expected)
    {
        var deposit = OrderDepositCalculator.CalculateDepositAmount(total, percent);

        Assert.Equal(expected, deposit);
    }

    [Fact]
    public void OrderDepositCalculator_WithInvalidPercent_UsesDefaultThirtyPercent()
    {
        var deposit = OrderDepositCalculator.CalculateDepositAmount(200m, 0);

        Assert.Equal(60m, deposit);
    }

    [Theory]
    [InlineData(1000, 300, 300, 700)]
    [InlineData(1000, 1200, 1200, 0)]
    [InlineData(500, -50, 0, 500)]
    public void OrderPaidAmountRecalculator_CalculatesRemainingAmount(
        decimal finalTotal,
        decimal summedPaid,
        decimal expectedPaid,
        decimal expectedRemaining)
    {
        var (paidAmount, remainingAmount) = OrderPaidAmountRecalculator.Calculate(finalTotal, summedPaid);

        Assert.Equal(expectedPaid, paidAmount);
        Assert.Equal(expectedRemaining, remainingAmount);
    }
}
