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

    [Fact]
    public void ResolvePostVatTotalAmount_WhenPreVatAndVatPresent_UsesPostVatSum()
    {
        var total = QuotationDepositCalculator.ResolvePostVatTotalAmount(
            subtotalAmount: 10_000_000m,
            totalDiscountAmount: 0m,
            preVatAmount: 10_000_000m,
            vatAmount: 800_000m,
            totalAmount: 10_000_000m);

        Assert.Equal(10_800_000m, total);
    }

    [Fact]
    public void ResolvePostVatTotalAmount_WhenPreVatMissing_UsesSubtotalMinusDiscountPlusVat()
    {
        var total = QuotationDepositCalculator.ResolvePostVatTotalAmount(
            subtotalAmount: 2_500_000m,
            totalDiscountAmount: 500_000m,
            preVatAmount: null,
            vatAmount: 160_000m,
            totalAmount: 2_700_000m);

        Assert.Equal(2_160_000m, total);
    }

    [Fact]
    public void ResolvePreVatAmount_WhenDiscountPresent_ReturnsNetPreVat()
    {
        var preVat = QuotationDepositCalculator.ResolvePreVatAmount(
            2_500_000m,
            500_000m,
            null);

        Assert.Equal(2_000_000m, preVat);
    }

    [Fact]
    public void CalculateDefaultDepositAmount_WithPostVatTotal10800000_Returns3240000()
    {
        var deposit = QuotationDepositCalculator.CalculateDefaultDepositAmount(10_800_000m, 30);

        Assert.Equal(3_240_000m, deposit);
    }
}
