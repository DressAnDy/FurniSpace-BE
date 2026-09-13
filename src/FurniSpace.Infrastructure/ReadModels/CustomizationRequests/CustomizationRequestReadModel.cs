using FurniSpace.Domain.Entities;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public class CustomizationRequestReadModel : CustomizationRequest
{
    public Guid CustomerId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
