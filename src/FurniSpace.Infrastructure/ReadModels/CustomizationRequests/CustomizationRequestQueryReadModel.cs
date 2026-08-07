using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class CustomizationRequestQueryReadModel
{
    public Guid ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? SourceProductVersionId { get; set; }
    public CustomizationStatus? Status { get; set; }
}
