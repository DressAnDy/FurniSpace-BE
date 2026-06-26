using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectFiles;

public sealed class ProjectFileSearchIndexItemReadModel
{
    public Guid FileId { get; set; }
    public Guid? FileLinkId { get; set; }
    public Guid ProjectId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public FileType? FileType { get; set; }
    public FileVisibility? Visibility { get; set; }
    public string? MimeType { get; set; }
    public DateTime? UploadedAt { get; set; }
    public FileStatus? Status { get; set; }
    public Guid? UploadedBy { get; set; }
}
