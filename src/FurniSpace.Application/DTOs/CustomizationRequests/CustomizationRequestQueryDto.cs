using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestQueryDto
{
    public Guid? ProposalId { get; set; }
    public Guid? ProposalItemId { get; set; }
    public CustomizationStatus? Status { get; set; }
}
