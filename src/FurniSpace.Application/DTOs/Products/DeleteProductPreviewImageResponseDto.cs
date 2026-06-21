namespace FurniSpace.Application.DTOs.Products;

public sealed class DeleteProductPreviewImageResponseDto
{
    public Guid FileId { get; init; }
    public Guid ProductId { get; init; }
    public DateTime DeletedAt { get; init; }
}
