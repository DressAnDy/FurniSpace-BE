using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public sealed class QuotationFinancialSummaryCalculator
{
    public QuotationFinancialSummary Calculate(IEnumerable<QuotationItem> items)
    {
        return items
            .Aggregate(QuotationFinancialSummary.Empty, static (summary, item) => summary.Add(item))
            .Round();
    }
}
