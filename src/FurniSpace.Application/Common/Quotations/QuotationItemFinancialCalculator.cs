using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public static class QuotationItemFinancialCalculator
{
    public static QuotationItem Calculate(QuotationItem item)
    {
        var quantity = item.Quantity ?? 0;
        var unitPrice = item.UnitPrice ?? 0m;
        var customizationCost = item.CustomizationAdditionalCost ?? 0m;
        var discountAmount = RoundMoney(item.DiscountAmount ?? 0m);
        var taxRate = RoundRate(item.TaxRate ?? 0m);

        var grossAmount = RoundMoney(quantity * (unitPrice + customizationCost));
        var taxableAmount = RoundMoney(grossAmount - discountAmount);
        var taxAmount = RoundMoney(taxableAmount * taxRate / 100m);

        item.UnitPrice = RoundMoney(unitPrice);
        item.CustomizationAdditionalCost = RoundMoney(customizationCost);
        item.GrossAmount = grossAmount;
        item.DiscountAmount = discountAmount;
        item.TaxableAmount = taxableAmount;
        item.TaxRate = taxRate;
        item.TaxAmount = taxAmount;
        item.TotalAmount = RoundMoney(taxableAmount + taxAmount);
        item.SubtotalAmount = grossAmount;

        return item;
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundRate(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
