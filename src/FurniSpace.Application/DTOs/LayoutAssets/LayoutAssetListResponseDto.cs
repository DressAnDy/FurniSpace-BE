namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class LayoutAssetListResponseDto
{
    public IReadOnlyList<LayoutAssetDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
