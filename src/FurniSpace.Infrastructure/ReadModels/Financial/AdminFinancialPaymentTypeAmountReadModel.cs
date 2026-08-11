using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialPaymentTypeAmountReadModel
{
    public PaymentType PaymentType { get; set; }
    public decimal Amount { get; set; }
}
