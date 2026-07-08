using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueQueryReadModel
{
    public IReadOnlyList<CustomizationStatus>? Statuses { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public bool? MaterialAvailable { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
