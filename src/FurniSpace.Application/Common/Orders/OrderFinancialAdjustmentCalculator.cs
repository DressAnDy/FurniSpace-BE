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
}
