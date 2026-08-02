namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CreateCustomizationRequestVersionResponseDto
{
    public Guid CustomizationRequestId { get; set; }
    public Guid CustomizationRequestVersionId { get; set; }
    public CustomizationRequestVersionDto Version { get; set; } = new();
}
