using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class OrderPaymentHistoryTransactionDto
{
    public Guid PaymentTransactionId { get; init; }
    public PaymentTransactionType? TransactionType { get; init; }
    public decimal Amount { get; init; }
    public PaymentTransactionStatus? Status { get; init; }
    public PaymentProvider? PaymentProvider { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string? ProviderReferenceCode { get; init; }
    public DateTime? TransactionTime { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class OrderPaymentHistoryPaymentDto
{
    public Guid PaymentId { get; init; }
    public string PaymentCode { get; init; } = string.Empty;
    public PaymentType? PaymentType { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public PaymentStatus? Status { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? ExpiredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public IReadOnlyList<OrderPaymentHistoryTransactionDto> Transactions { get; init; } = [];
}

public sealed class OrderPaymentHistoryResponseDto
{
    public Guid OrderId { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal? DepositAmount { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal? RemainingAmount { get; init; }
    public IReadOnlyList<OrderPaymentHistoryPaymentDto> Payments { get; init; } = [];
}

public sealed class OrderPaymentHistoryQueryDto
{
    public PaymentStatus? Status { get; set; }
    public PaymentType? PaymentType { get; set; }
}
