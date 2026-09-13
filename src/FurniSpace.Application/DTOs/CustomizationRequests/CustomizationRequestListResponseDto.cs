namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestListResponseDto
{
    public IReadOnlyList<CustomizationRequestDto> Items { get; set; } = [];
}
