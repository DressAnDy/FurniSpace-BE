using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public static class QuotationItemFinancialCalculator
{
    public static QuotationItem Calculate(QuotationItem item)
    {
        var quantity = item.Quantity ?? 0;
        var unitPrice = RoundMoney(item.UnitPrice ?? 0m);
        var discountAmount = RoundMoney(item.DiscountAmount ?? 0m);
        var grossAmount = RoundMoney(quantity * unitPrice);

        item.UnitPrice = unitPrice;
        item.GrossAmount = grossAmount;
        item.DiscountAmount = discountAmount;
        item.TotalAmount = RoundMoney(grossAmount - discountAmount);

        return item;
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
