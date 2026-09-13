using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class CreatePaymentTransactionAttemptRequestDto
{
    public PaymentProvider PaymentProvider { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public sealed class PaymentTransactionAttemptResponseDto
{
    public Guid PaymentTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentTransactionStatus? Status { get; set; }
    public PaymentProvider? PaymentProvider { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrContent { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
}
