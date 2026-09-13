#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestListResponseDto
{
    public List<ProductionRequestListItemDto> Items { get; set; } = [];
}
