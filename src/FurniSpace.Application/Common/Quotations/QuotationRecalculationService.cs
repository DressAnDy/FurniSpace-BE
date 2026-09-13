using FurniSpace.Application.Constants.Financial;
using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public sealed class QuotationRecalculationService
{
    public static Quotation Recalculate(
        Quotation quotation,
        IEnumerable<QuotationItem> persistedItems,
        QuotationItem? pendingItem = null,
        Guid? excludedItemId = null)
    {
        var items = persistedItems
            .Where(item => item.QuotationItemId != excludedItemId)
            .ToList();

        if (pendingItem is not null)
        {
            items.Add(pendingItem);
        }

        foreach (var item in items)
        {
            QuotationItemFinancialCalculator.Calculate(item);
        }

        var vatRate = quotation.VatRate ?? FinancialConstants.DefaultVatRate;
        var summary = QuotationFinancialSummaryCalculator.Calculate(items)
            .WithHeaderVat(vatRate)
            .Round();

        quotation.SubtotalAmount = summary.SubtotalAmount;
        quotation.TotalDiscountAmount = summary.TotalDiscountAmount;
        quotation.PreVatAmount = summary.PreVatAmount;
        quotation.VatRate = vatRate;
        quotation.VatAmount = summary.VatAmount;
        quotation.TotalAmount = summary.TotalAmount;
        quotation.Currency = string.IsNullOrWhiteSpace(quotation.Currency) ? "VND" : quotation.Currency;

        return quotation;
    }
}
