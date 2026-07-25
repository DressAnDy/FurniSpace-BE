#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderFinalPaymentPreparationDto
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal FinalTotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool RequiresRemainingPayment { get; set; }
}
