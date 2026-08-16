namespace FurniSpace.Application.Common.Quotations;

public static class QuotationDepositCalculator
{
    public static decimal CalculateDefaultDepositAmount(decimal totalAmount, int depositPercent)
    {
        if (totalAmount <= 0m)
        {
            return 0m;
        }

        var percent = depositPercent is > 0 and <= 100 ? depositPercent : 30;
        return decimal.Truncate(totalAmount * percent / 100m);
    }
}
