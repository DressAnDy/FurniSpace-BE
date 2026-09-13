namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class ReorderProductVersionPreviewFilesRequestDto
{
    public IReadOnlyList<Guid>? FileIds { get; init; }
}
