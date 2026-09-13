using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public readonly record struct QuotationFinancialSummary(
    decimal SubtotalAmount,
    decimal TotalDiscountAmount,
    decimal PreVatAmount,
    decimal VatAmount,
    decimal TotalAmount)
{
    public static QuotationFinancialSummary Empty { get; } = new(0m, 0m, 0m, 0m, 0m);

    public QuotationFinancialSummary Add(QuotationItem item)
    {
        return new QuotationFinancialSummary(
            SubtotalAmount + (item.GrossAmount ?? 0m),
            TotalDiscountAmount + (item.DiscountAmount ?? 0m),
            PreVatAmount + (item.TotalAmount ?? 0m),
            VatAmount,
            TotalAmount + (item.TotalAmount ?? 0m));
    }

    public QuotationFinancialSummary WithHeaderVat(decimal vatRate)
    {
        var vatAmount = RoundMoney(PreVatAmount * vatRate);
        return new QuotationFinancialSummary(
            SubtotalAmount,
            TotalDiscountAmount,
            PreVatAmount,
            vatAmount,
            PreVatAmount + vatAmount);
    }

    public QuotationFinancialSummary Round()
    {
        return new QuotationFinancialSummary(
            RoundMoney(SubtotalAmount),
            RoundMoney(TotalDiscountAmount),
            RoundMoney(PreVatAmount),
            RoundMoney(VatAmount),
            RoundMoney(TotalAmount));
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
