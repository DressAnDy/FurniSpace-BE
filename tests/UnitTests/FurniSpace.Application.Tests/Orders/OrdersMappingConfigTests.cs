using System;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Mappings;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using Mapster;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrdersMappingConfigTests
{
    public OrdersMappingConfigTests()
    {
        var config = new TypeAdapterConfig();
        new OrdersMappingConfig().Register(config);
        config.Compile();
        _config = config;
    }

    private readonly TypeAdapterConfig _config;

    [Fact]
    public void Adapt_OrderListItemReadModel_MapsToOrderListItemDto()
    {
        var source = new OrderListItemReadModel
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            OriginalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            CreatedAt = DateTime.UtcNow
        };

        var result = source.Adapt<OrderListItemDto>(_config);

        Assert.Equal(source.OrderId, result.OrderId);
        Assert.Equal(source.OrderCode, result.OrderCode);
        Assert.Equal(source.Status, result.Status);
    }

    [Fact]
    public void Adapt_OrderDetailReadModel_MapsItemsToOrderItemDto()
    {
        var itemId = Guid.NewGuid();
        var source = new OrderDetailReadModel
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-002",
            CustomerId = Guid.NewGuid(),
            FinalTotalAmount = 250m,
            Status = OrderStatus.DEPOSIT_PENDING,
            Items =
            [
                new OrderItemDetailReadModel
                {
                    OrderItemId = itemId,
                    ItemName = "Counter",
                    Quantity = 2,
                    Status = OrderItemStatus.READY,
                    DeliveredQuantity = 1,
                    CustomerConfirmedAt = null,
                    UnitPrice = 50m,
                    CustomizationUnitAdditionalCost = 5m,
                    CustomizationAdditionalCost = 5m,
                    GrossAmount = 110m,
                    DiscountAmount = 10m,
                    TaxableAmount = 100m,
                    TaxRate = 8m,
                    TaxAmount = 8m,
                    TotalAmount = 108m,
                    SubtotalAmount = 100m
                }
            ]
        };

        var result = source.Adapt<OrderDetailDto>(_config);

        Assert.Equal(source.OrderId, result.OrderId);
        var item = Assert.Single(result.Items);
        Assert.Equal(itemId, item.OrderItemId);
        Assert.Equal("Counter", item.ItemName);
        Assert.Equal(OrderItemStatus.READY, item.Status);
        Assert.Equal(1, item.DeliveredQuantity);
        Assert.Null(item.CustomerConfirmedAt);
        Assert.Equal(50m, item.UnitPrice);
        Assert.Equal(5m, item.CustomizationUnitAdditionalCost);
        Assert.Equal(5m, item.CustomizationAdditionalCost);
        Assert.Equal(110m, item.GrossAmount);
        Assert.Equal(10m, item.DiscountAmount);
        Assert.Equal(100m, item.TaxableAmount);
        Assert.Equal(8m, item.TaxRate);
        Assert.Equal(8m, item.TaxAmount);
        Assert.Equal(108m, item.TotalAmount);
    }

    [Fact]
    public void PaymentDetailDto_From_MapsAllPaymentFields()
    {
        var paymentId = Guid.NewGuid();
        var source = new PaymentDto
        {
            PaymentId = paymentId,
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = 100m,
            Status = PaymentStatus.PENDING
        };

        var result = PaymentDetailDto.From(source);

        Assert.Equal(paymentId, result.PaymentId);
        Assert.Equal(source.PaymentCode, result.PaymentCode);
        Assert.Equal(source.Amount, result.Amount);
    }
}
