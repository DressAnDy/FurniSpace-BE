using System;
using FurniSpace.Application.Common.Quotations;
using FurniSpace.Domain.Entities;
using Xunit;

namespace FurniSpace.Application.Tests.Quotations;

public sealed class QuotationCommercialLineAggregatorTests
{
    private static readonly Guid ProductVersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void AggregateFromProposalItems_WhenTwoEquivalentItemsWithQtyOne_ProducesOneLineWithQtyTwo()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m),
            CreateProposalItem(quantity: 1, unitPrice: 100m)
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Single(result);
        Assert.Equal(2, result[0].Quantity);
        Assert.Null(result[0].ProposalItemId);
        QuotationItemFinancialCalculator.Calculate(result[0]);
        Assert.Equal(200m, result[0].GrossAmount);
        Assert.Equal(200m, result[0].TotalAmount);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenSourceQuantitiesAreOneOneTwo_ProducesOneLineWithQtyFour()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 50m),
            CreateProposalItem(quantity: 1, unitPrice: 50m),
            CreateProposalItem(quantity: 2, unitPrice: 50m)
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Single(result);
        Assert.Equal(4, result[0].Quantity);
        QuotationItemFinancialCalculator.Calculate(result[0]);
        Assert.Equal(200m, result[0].GrossAmount);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenSingleSource_RetainsProposalItemId()
    {
        var proposalItem = CreateProposalItem(quantity: 2, unitPrice: 100m);

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            [proposalItem]);

        Assert.Single(result);
        Assert.Equal(proposalItem.ProposalItemId, result[0].ProposalItemId);
        Assert.Equal(2, result[0].Quantity);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenDifferentProductVersions_RemainSeparate()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m, productVersionId: Guid.NewGuid()),
            CreateProposalItem(quantity: 1, unitPrice: 100m, productVersionId: Guid.NewGuid())
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenSameProductWithDifferentUnitPrice_RemainSeparate()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m),
            CreateProposalItem(quantity: 1, unitPrice: 120m)
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenCustomizedAndStandard_RemainSeparate()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m, isCustomized: false),
            CreateProposalItem(quantity: 1, unitPrice: 100m, isCustomized: true, note: "Wood")
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenDifferentCustomizationNotes_RemainSeparate()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m, isCustomized: true, note: "Oak"),
            CreateProposalItem(quantity: 1, unitPrice: 100m, isCustomized: true, note: "Walnut")
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenDifferentPerUnitDiscount_RemainSeparate()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m, totalPriceSnapshot: 90m),
            CreateProposalItem(quantity: 1, unitPrice: 100m, totalPriceSnapshot: 80m)
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AggregateFromProposalItems_WhenCompatiblePerUnitDiscount_SumsDiscountAmount()
    {
        var proposalItems = new[]
        {
            CreateProposalItem(quantity: 1, unitPrice: 100m, totalPriceSnapshot: 90m),
            CreateProposalItem(quantity: 2, unitPrice: 100m, totalPriceSnapshot: 180m)
        };

        var result = QuotationCommercialLineAggregator.AggregateFromProposalItems(
            Guid.NewGuid(),
            proposalItems);

        Assert.Single(result);
        Assert.Equal(3, result[0].Quantity);
        QuotationItemFinancialCalculator.Calculate(result[0]);
        Assert.Equal(300m, result[0].GrossAmount);
        Assert.Equal(30m, result[0].DiscountAmount);
        Assert.Equal(270m, result[0].TotalAmount);
    }

    private static ProposalItem CreateProposalItem(
        int quantity,
        decimal unitPrice,
        decimal? totalPriceSnapshot = null,
        Guid? productVersionId = null,
        bool isCustomized = false,
        string? note = null)
    {
        var grossAmount = quantity * unitPrice;
        return new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProductVersionId = productVersionId ?? ProductVersionId,
            ItemName = "Commercial item",
            Quantity = quantity,
            UnitPriceSnapshot = unitPrice,
            TotalPriceSnapshot = totalPriceSnapshot ?? grossAmount,
            IsCustomized = isCustomized,
            Note = note
        };
    }
}
