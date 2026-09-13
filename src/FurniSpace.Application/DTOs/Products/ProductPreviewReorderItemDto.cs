namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductPreviewReorderItemDto
{
    public Guid FileId { get; init; }
    public Guid FileLinkId { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsPrimary { get; init; }
}
