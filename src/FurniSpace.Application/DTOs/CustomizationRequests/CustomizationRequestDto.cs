using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public class CustomizationRequestDto : CustomizationRequest
{
    public const string ResourceName = "customizationRequest";

    public ApprovedProductVersionSummaryDto? SourceProductVersion { get; set; }

    public CustomizationRequestVersionDto? AcceptedVersion { get; set; }

    public new IReadOnlyList<CustomizationRequestVersionDto> Versions { get; set; } = [];
}
