namespace FurniSpace.Application.DTOs.Payments;

public sealed class CreateProjectStartFeePaymentRequestDto
{
    public decimal? Amount { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string? Note { get; set; }
}
