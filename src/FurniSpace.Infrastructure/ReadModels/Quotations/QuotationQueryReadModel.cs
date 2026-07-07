using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Quotations;

public sealed class QuotationQueryReadModel
{
    public Guid ProjectId { get; set; }
    public QuotationStatus? Status { get; set; }
}
