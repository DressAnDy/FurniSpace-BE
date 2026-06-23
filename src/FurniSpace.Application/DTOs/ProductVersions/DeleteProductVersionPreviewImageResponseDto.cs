namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class DeleteProductVersionPreviewImageResponseDto
{
    public Guid DeletedFileId { get; init; }
    public int RemainingCount { get; init; }
    public bool Reindexed { get; init; }
}
