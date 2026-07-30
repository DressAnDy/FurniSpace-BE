namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestDetailDto : CustomizationRequestDto
{
    public ApprovedProductVersionSummaryDto? SourceProductVersion { get; set; }
}
