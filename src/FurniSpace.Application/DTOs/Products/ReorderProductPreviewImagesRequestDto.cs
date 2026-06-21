namespace FurniSpace.Application.DTOs.Products;

public sealed class ReorderProductPreviewImagesRequestDto
{
    public IReadOnlyList<Guid>? FileIds { get; init; }
    public IReadOnlyList<ReorderProductPreviewImageItemDto>? Items { get; init; }
}

public sealed class ReorderProductPreviewImageItemDto
{
    public Guid FileId { get; init; }
    public int DisplayOrder { get; init; }
}
