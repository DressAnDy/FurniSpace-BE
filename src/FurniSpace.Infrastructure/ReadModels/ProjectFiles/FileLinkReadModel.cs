using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectFiles;

public sealed class FileLinkReadModel
{
    public Guid FileLinkId { get; init; }
    public Guid FileId { get; init; }
    public string ReferenceType { get; init; } = string.Empty;
    public Guid ReferenceId { get; init; }
    public FileType? FileType { get; init; }
    public FileVisibility? Visibility { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid UploadedBy { get; init; }
    public ProjectFileAccessReadModel? ProjectAccess { get; init; }
}
