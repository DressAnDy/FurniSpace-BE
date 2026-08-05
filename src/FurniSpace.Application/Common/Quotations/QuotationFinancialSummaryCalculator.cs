using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public static class QuotationFinancialSummaryCalculator
{
    public static QuotationFinancialSummary Calculate(IEnumerable<QuotationItem> items)
    {
        return items
            .Aggregate(QuotationFinancialSummary.Empty, static (summary, item) => summary.Add(item))
            .Round();
    }
}
