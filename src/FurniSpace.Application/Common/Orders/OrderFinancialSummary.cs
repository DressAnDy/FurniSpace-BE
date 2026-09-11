using FurniSpace.Infrastructure.ReadModels.Orders;

namespace FurniSpace.Application.Common.Orders;

public readonly record struct OrderFinancialSummary(
    decimal ItemsGrossAmount,
    decimal TotalItemDiscountAmount,
    decimal PreVatAmount)
{
    public static OrderFinancialSummary FromItems(IReadOnlyList<OrderItemDetailReadModel> items)
    {
        decimal gross = 0m;
        decimal discount = 0m;

        foreach (var item in items)
        {
            var quantity = item.Quantity ?? 0;
            gross += quantity * (item.UnitPrice ?? 0m);
            discount += item.DiscountAmount ?? 0m;
        }

        var preVat = gross - discount;
        return new OrderFinancialSummary(
            RoundMoney(gross),
            RoundMoney(discount),
            RoundMoney(preVat));
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
