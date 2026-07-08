namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionCustomizationRequestListResponseDto
{
    public IReadOnlyList<ProductionCustomizationRequestQueueItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
