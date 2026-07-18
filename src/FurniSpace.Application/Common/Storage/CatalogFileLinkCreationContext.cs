using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common;

internal sealed record CatalogFileLinkCreationContext
{
    public required Guid FileLinkId { get; init; }

    public required Guid FileId { get; init; }

    public required string ReferenceType { get; init; }

    public required Guid ReferenceId { get; init; }

    public required FileType FileType { get; init; }

    public required FileVisibility Visibility { get; init; }

    public required Guid CreatedBy { get; init; }

    public required DateTime CreatedAt { get; init; }

    public string? Description { get; init; }

    public int? DisplayOrder { get; init; }
}
