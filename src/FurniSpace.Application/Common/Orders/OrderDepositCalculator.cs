namespace FurniSpace.Application.Common.Orders;

public static class OrderDepositCalculator
{
    public static decimal CalculateDepositAmount(decimal finalTotalAmount, int depositPercent)
    {
        if (finalTotalAmount <= 0m)
        {
            return 0m;
        }

        var percent = depositPercent is > 0 and <= 100 ? depositPercent : 30;
        return decimal.Truncate(finalTotalAmount * percent / 100m);
    }
}
