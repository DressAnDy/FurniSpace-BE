namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestVersionListResponseDto
{
    public IReadOnlyList<CustomizationRequestVersionDto> Items { get; set; } = [];
}
