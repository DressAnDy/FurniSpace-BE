using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectFiles;

public sealed class ProjectLinkedFileReadModel
{
    public Guid FileId { get; init; }
    public FileType? FileType { get; init; }
    public string MimeType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
}
