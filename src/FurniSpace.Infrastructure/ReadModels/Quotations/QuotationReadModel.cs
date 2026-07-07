using FurniSpace.Domain.Entities;

namespace FurniSpace.Infrastructure.ReadModels.Quotations;

public class QuotationReadModel : Quotation
{
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
