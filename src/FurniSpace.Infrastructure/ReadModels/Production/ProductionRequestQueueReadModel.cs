#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Production;

public sealed class ProductionRequestQueueReadModel
{
    public Guid? ProjectId { get; set; }
    public ProductionRequestStatus? Status { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? Priority { get; set; }
    public string? CurrentUserRole { get; set; }
    public Guid CurrentUserId { get; set; }
}
