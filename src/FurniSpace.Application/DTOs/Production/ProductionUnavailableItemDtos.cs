#nullable enable

using FurniSpace.Domain.Common;

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionUnavailableItemsQueryDto
{
    public string? Keyword { get; set; }
    public Guid? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ProductionUnavailableItemsResponseDto
{
    public List<ProductionUnavailableItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ProductionUnavailableItemDto : ProductionUnavailableItemShape;
