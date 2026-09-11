namespace FurniSpace.Application.Common.Quotations;

public static class QuotationDepositCalculator
{
    public static decimal ResolvePostVatTotalAmount(
        decimal? subtotalAmount,
        decimal? totalDiscountAmount,
        decimal? preVatAmount,
        decimal? vatAmount,
        decimal? totalAmount)
    {
        var preVat = ResolvePreVatAmount(subtotalAmount, totalDiscountAmount, preVatAmount);
        var vat = vatAmount ?? 0m;
        if (preVat > 0m || vat > 0m)
        {
            return preVat + vat;
        }

        return totalAmount ?? 0m;
    }

    public static decimal ResolvePreVatAmount(
        decimal? subtotalAmount,
        decimal? totalDiscountAmount,
        decimal? preVatAmount)
    {
        var preVat = preVatAmount ?? 0m;
        if (preVat > 0m)
        {
            return preVat;
        }

        var netPreVat = (subtotalAmount ?? 0m) - (totalDiscountAmount ?? 0m);
        return netPreVat > 0m ? netPreVat : 0m;
    }

    public static decimal CalculateDefaultDepositAmount(decimal postVatTotalAmount, int depositPercent)
    {
        if (postVatTotalAmount <= 0m)
        {
            return 0m;
        }

        var percent = depositPercent is > 0 and <= 100 ? depositPercent : 30;
        return decimal.Truncate(postVatTotalAmount * percent / 100m);
    }
}
