namespace FurniSpace.Application.DTOs.Products;

public sealed class DeleteProductPreviewImageResponseDto
{
    public Guid DeletedFileId { get; init; }
    public int RemainingCount { get; init; }
    public bool Reindexed { get; init; }
}
