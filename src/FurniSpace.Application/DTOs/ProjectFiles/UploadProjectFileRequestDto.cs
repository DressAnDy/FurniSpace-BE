using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class UploadProjectFileRequestDto
{
    public Stream Content { get; init; } = Stream.Null;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public FileType FileType { get; init; } = FileType.OTHER;
    public FileVisibility? Visibility { get; init; }
    public bool? IsPrimary { get; init; }
    public int? DisplayOrder { get; init; }
    public string? Note { get; init; }
}
