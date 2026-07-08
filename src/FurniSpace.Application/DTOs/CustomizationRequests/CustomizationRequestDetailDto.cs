namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestDetailDto : CustomizationRequestDto
{
    public CustomizationRequestItemSnapshotDto ProposalItem { get; set; } = new();
}
