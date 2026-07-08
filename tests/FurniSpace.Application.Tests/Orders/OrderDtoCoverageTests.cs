using System;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderDtoCoverageTests
{
    [Fact]
    public void OrderListResponseDto_StoresItems()
    {
        var orderId = Guid.NewGuid();
        var response = new OrderListResponseDto
        {
            Items =
            [
                new OrderListItemDto
                {
                    OrderId = orderId,
                    ProjectId = Guid.NewGuid(),
                    QuotationId = Guid.NewGuid(),
                    OrderCode = "ORD-001",
                    OriginalTotalAmount = 100m,
                    DepositAmount = 30m,
                    Status = OrderStatus.DEPOSIT_PENDING
                }
            ]
        };

        Assert.Single(response.Items);
        Assert.Equal(orderId, response.Items[0].OrderId);
    }

    [Fact]
    public void CreateOrderPaymentRequestDtos_StoreOptionalFields()
    {
        var expiredAt = DateTime.UtcNow.AddDays(1);
        var deposit = new CreateOrderDepositPaymentRequestDto
        {
            ExpiredAt = expiredAt,
            Note = "Deposit"
        };
        var remaining = new CreateOrderRemainingPaymentRequestDto
        {
            ExpiredAt = expiredAt,
            Note = "Remaining"
        };

        Assert.Equal(expiredAt, deposit.ExpiredAt);
        Assert.Equal("Deposit", deposit.Note);
        Assert.Equal("Remaining", remaining.Note);
    }
}
