using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public readonly record struct QuotationFinancialSummary(
    decimal SubtotalAmount,
    decimal TotalDiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal TotalAmount)
{
    public static QuotationFinancialSummary Empty { get; } = new(0m, 0m, 0m, 0m, 0m);

    public QuotationFinancialSummary Add(QuotationItem item)
    {
        return new QuotationFinancialSummary(
            SubtotalAmount + (item.GrossAmount ?? 0m),
            TotalDiscountAmount + (item.DiscountAmount ?? 0m),
            TaxableAmount + (item.TaxableAmount ?? 0m),
            TaxAmount + (item.TaxAmount ?? 0m),
            TotalAmount + (item.TotalAmount ?? 0m));
    }

    public QuotationFinancialSummary Round()
    {
        return new QuotationFinancialSummary(
            RoundMoney(SubtotalAmount),
            RoundMoney(TotalDiscountAmount),
            RoundMoney(TaxableAmount),
            RoundMoney(TaxAmount),
            RoundMoney(TotalAmount));
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
