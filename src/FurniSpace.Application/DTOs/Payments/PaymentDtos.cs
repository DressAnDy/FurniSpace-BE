using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Payments;

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? QuotationId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public Guid? PaidBy { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus? Status { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PaymentListResponseDto
{
    public IReadOnlyList<PaymentDto> Items { get; init; } = [];
}

public sealed class PaymentTransactionDto
{
    public Guid PaymentTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public PaymentTransactionType? TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentProvider? PaymentProvider { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReferenceCode { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public DateTime? TransactionTime { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class PaymentTransactionListResponseDto
{
    public IReadOnlyList<PaymentTransactionDto> Items { get; init; } = [];
}

public sealed class PaymentStatusByCodeDto
{
    public Guid PaymentId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public PaymentStatus? Status { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class CreateTestPaymentRequestDto
{
    public Guid ProjectId { get; set; }
    public decimal Amount { get; set; }
    public PaymentType PaymentType { get; set; } = PaymentType.OTHER;
    public string? Note { get; set; }
    public DateTime? ExpiredAt { get; set; }
}

public sealed class PaymentQueryDto
{
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public PaymentStatus? Status { get; set; }
    public PaymentType? PaymentType { get; set; }
}

public sealed class SePayVietQrResponseDto
{
    public Guid PaymentId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string Provider { get; set; } = "SEPAY";
    public string Method { get; set; } = "QR_CODE";
    public decimal Amount { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public string VietQrUrl { get; set; } = string.Empty;
    public PaymentStatus? Status { get; set; }
}

public sealed class SePayWebhookPayloadDto
{
    public long Id { get; set; }
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? SubAccount { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    public string? TransferType { get; set; }
    public string? Description { get; set; }
    public decimal TransferAmount { get; set; }
    public decimal? Accumulated { get; set; }
    public string? ReferenceCode { get; set; }
}

public sealed class SePayWebhookSuccessDto
{
    public bool Success { get; set; } = true;
}

public sealed class CreatePayOsPaymentLinkRequestDto
{
    public decimal? Amount { get; set; }
    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public sealed class PayOsPaymentLinkResponseDto
{
    public Guid PaymentId { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string Provider { get; set; } = "PAYOS";
    public string Method { get; set; } = "PAYMENT_LINK";
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
}

public sealed class PayOsConfirmWebhookRequestDto
{
    public string WebhookUrl { get; set; } = string.Empty;
}

public sealed class PayOsConfirmWebhookResponseDto
{
    public bool Success { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
}

public sealed class PayOsWebhookSuccessDto
{
    public bool Success { get; set; } = true;
}
