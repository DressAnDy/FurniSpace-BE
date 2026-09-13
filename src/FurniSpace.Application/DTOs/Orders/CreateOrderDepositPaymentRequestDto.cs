namespace FurniSpace.Application.DTOs.Orders;

public sealed class CreateOrderDepositPaymentRequestDto
{
    public DateTime? ExpiredAt { get; set; }
    public string? Note { get; set; }
}
