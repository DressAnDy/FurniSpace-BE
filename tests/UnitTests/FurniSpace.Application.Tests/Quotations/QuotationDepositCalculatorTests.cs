#nullable enable

using FurniSpace.Application.Common.Quotations;
using Xunit;

namespace FurniSpace.Application.Tests.Quotations;

public sealed class QuotationDepositCalculatorTests
{
    [Fact]
    public void CalculateDefaultDepositAmount_With30Percent_TruncatesFraction()
    {
        var deposit = QuotationDepositCalculator.CalculateDefaultDepositAmount(216m, 30);

        Assert.Equal(64m, deposit);
    }

    [Fact]
    public void CalculateDefaultDepositAmount_With21600Total_Returns6480()
    {
        var deposit = QuotationDepositCalculator.CalculateDefaultDepositAmount(21_600m, 30);

        Assert.Equal(6_480m, deposit);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-100, 0)]
    public void CalculateDefaultDepositAmount_WhenTotalNotPositive_ReturnsZero(decimal total, decimal expected)
    {
        var deposit = QuotationDepositCalculator.CalculateDefaultDepositAmount(total, 30);

        Assert.Equal(expected, deposit);
    }

    [Fact]
    public void CalculateDefaultDepositAmount_WhenPercentInvalid_UsesDefault30()
    {
        var deposit = QuotationDepositCalculator.CalculateDefaultDepositAmount(100m, 0);

        Assert.Equal(30m, deposit);
    }
}
