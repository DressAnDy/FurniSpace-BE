namespace FurniSpace.Application.DTOs.Payments;

public sealed class PaymentDetailDto : PaymentDto
{
    public static PaymentDetailDto From(PaymentDto source)
    {
        return new PaymentDetailDto
        {
            PaymentId = source.PaymentId,
            ProjectId = source.ProjectId,
            OrderId = source.OrderId,
            QuotationId = source.QuotationId,
            PaymentCode = source.PaymentCode,
            PaidBy = source.PaidBy,
            PaymentType = source.PaymentType,
            Amount = source.Amount,
            PaidAmount = source.PaidAmount,
            RemainingAmount = source.RemainingAmount,
            Currency = source.Currency,
            Status = source.Status,
            ExpiredAt = source.ExpiredAt,
            PaidAt = source.PaidAt,
            CancelledAt = source.CancelledAt,
            Note = source.Note,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }
}
