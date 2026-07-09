namespace FurniSpace.Application.DTOs.Orders;

public sealed class UpdateOrderFinancialAdjustmentRequestDto
{
    public decimal? AdditionalDiscountAmount { get; set; }

    public decimal? DepositAmount { get; set; }

    public string? AdjustmentNote { get; set; }
}
