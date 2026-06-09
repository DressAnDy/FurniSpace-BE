namespace FurniSpace.Infrastructure.DTOs.ProjectFiles;

public sealed class FileReferencePageReadModel
{
    public IReadOnlyList<FileMetadataReadModel> Items { get; init; } = Array.Empty<FileMetadataReadModel>();
    public int Total { get; init; }
}
