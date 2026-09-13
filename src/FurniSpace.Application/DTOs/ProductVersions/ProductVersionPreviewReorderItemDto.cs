namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class ProductVersionPreviewReorderItemDto
{
    public Guid FileId { get; init; }
    public Guid FileLinkId { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsPrimary { get; init; }
}
