using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestQueryDto
{
    public Guid? ProposalId { get; set; }
    public Guid? SourceProductVersionId { get; set; }
    public CustomizationStatus? Status { get; set; }
}
