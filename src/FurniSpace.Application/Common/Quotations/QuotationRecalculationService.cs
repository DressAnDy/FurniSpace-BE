using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public sealed class QuotationRecalculationService(
    QuotationItemFinancialCalculator itemCalculator,
    QuotationFinancialSummaryCalculator summaryCalculator)
{
    public Quotation Recalculate(
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
            itemCalculator.Calculate(item);
        }

        var summary = summaryCalculator.Calculate(items);
        quotation.SubtotalAmount = summary.SubtotalAmount;
        quotation.DiscountAmount = summary.TotalDiscountAmount;
        quotation.TaxableAmount = summary.TaxableAmount;
        quotation.TaxAmount = summary.TaxAmount;
        quotation.TotalAmount = summary.TotalAmount;
        quotation.Currency = string.IsNullOrWhiteSpace(quotation.Currency) ? "VND" : quotation.Currency;

        return quotation;
    }
}
