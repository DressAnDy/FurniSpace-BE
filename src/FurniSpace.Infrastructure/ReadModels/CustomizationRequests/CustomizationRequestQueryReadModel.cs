using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class CustomizationRequestQueryReadModel
{
    public Guid ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? ProposalItemId { get; set; }
    public CustomizationStatus? Status { get; set; }
}
