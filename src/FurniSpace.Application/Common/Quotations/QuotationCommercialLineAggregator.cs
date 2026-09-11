using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

public static class QuotationCommercialLineAggregator
{
    public static List<QuotationItem> AggregateFromProposalItems(
        Guid quotationId,
        IReadOnlyList<ProposalItem> proposalItems)
    {
        var now = DateTime.UtcNow;
        var sources = proposalItems
            .Select(FromProposalItem)
            .ToList();

        return sources
            .GroupBy(source => source.Key)
            .Select((group, index) => ToAggregatedQuotationItem(
                quotationId,
                group.ToList(),
                index,
                now))
            .ToList();
    }

    private static ProposalCommercialLineSource FromProposalItem(ProposalItem item)
    {
        var quantity = item.Quantity ?? 0;
        var unitPrice = RoundMoney(item.UnitPriceSnapshot ?? 0m);
        var grossAmount = RoundMoney(quantity * unitPrice);
        var linePreVatTotal = item.TotalPriceSnapshot ?? grossAmount;
        var lineDiscountAmount = grossAmount > linePreVatTotal
            ? RoundMoney(grossAmount - linePreVatTotal)
            : 0m;
        var perUnitDiscount = ComputePerUnitDiscount(quantity, lineDiscountAmount);
        var isCustomized = item.IsCustomized ?? false;
        var customizationNote = isCustomized
            ? NormalizeCustomizationNote(item.Note)
            : string.Empty;

        return new ProposalCommercialLineSource(
            item,
            quantity,
            unitPrice,
            lineDiscountAmount,
            new CommercialLineKey(
                item.ProductVersionId,
                unitPrice,
                isCustomized,
                customizationNote,
                perUnitDiscount));
    }

    private static QuotationItem ToAggregatedQuotationItem(
        Guid quotationId,
        IReadOnlyList<ProposalCommercialLineSource> sources,
        int displayOrder,
        DateTime timestamp)
    {
        var primary = sources[0];
        var aggregatedQuantity = sources.Sum(source => source.Quantity);
        var aggregatedDiscount = RoundMoney(sources.Sum(source => source.LineDiscountAmount));
        var proposalItemId = sources.Count == 1
            ? primary.Item.ProposalItemId
            : (Guid?)null;

        return new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ProposalItemId = proposalItemId,
            ProductVersionId = primary.Key.ProductVersionId,
            ProductNameSnapshot = primary.Item.ItemName,
            ProductVersionNameSnapshot = primary.Item.ItemName,
            ItemName = primary.Item.ItemName,
            DisplayOrder = displayOrder,
            Quantity = aggregatedQuantity,
            UnitPrice = primary.Key.UnitPrice,
            DiscountAmount = aggregatedDiscount,
            IsCustomized = primary.Key.IsCustomized,
            CustomizationNote = primary.Key.IsCustomized ? primary.Item.Note : null,
            Note = primary.Item.Note,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static decimal ComputePerUnitDiscount(int quantity, decimal lineDiscountAmount)
    {
        if (quantity <= 0)
        {
            return 0m;
        }

        return RoundMoney(lineDiscountAmount / quantity);
    }

    private static string NormalizeCustomizationNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : note.Trim();
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record CommercialLineKey(
        Guid? ProductVersionId,
        decimal UnitPrice,
        bool IsCustomized,
        string CustomizationNote,
        decimal PerUnitDiscount);

    private sealed record ProposalCommercialLineSource(
        ProposalItem Item,
        int Quantity,
        decimal UnitPrice,
        decimal LineDiscountAmount,
        CommercialLineKey Key);
}
