namespace FurniSpace.Application.Common.Orders;

internal static class OrderFinancialAdjustmentCalculator
{
    public static decimal CalculateBaseBeforeAdditionalDiscount(
        decimal originalTotalAmount,
        decimal itemAdjustmentAmount)
    {
        return originalTotalAmount - itemAdjustmentAmount;
    }

    public static decimal CalculateFinalTotalAmount(
        decimal baseBeforeAdditionalDiscount,
        decimal additionalDiscountAmount)
    {
        return baseBeforeAdditionalDiscount - additionalDiscountAmount;
    }

    public static decimal CalculateVatInclusiveUnavailableAdjustment(
        decimal itemPreVatAmount,
        decimal orderVatRate)
    {
        var vatShare = RoundMoney(itemPreVatAmount * orderVatRate);
        return itemPreVatAmount + vatShare;
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
