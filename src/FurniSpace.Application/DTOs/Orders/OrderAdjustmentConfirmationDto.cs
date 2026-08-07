#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderAdjustmentConfirmationDto
{
    public Guid OrderAdjustmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
