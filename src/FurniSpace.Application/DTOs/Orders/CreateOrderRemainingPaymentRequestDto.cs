namespace FurniSpace.Application.DTOs.Orders;

public sealed class CreateOrderRemainingPaymentRequestDto
{
    public DateTime? ExpiredAt { get; set; }
    public string? Note { get; set; }
}
