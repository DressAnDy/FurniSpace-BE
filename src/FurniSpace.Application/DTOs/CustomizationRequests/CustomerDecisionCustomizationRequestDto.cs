namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomerDecisionCustomizationRequestDto
{
    public string Decision { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
}
