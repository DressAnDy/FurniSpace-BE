using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectFiles;

public sealed class FileReferenceQueryReadModel
{
    public string ReferenceType { get; init; } = string.Empty;
    public Guid ReferenceId { get; init; }
    public FileType? FileType { get; init; }
    public FileVisibility? Visibility { get; init; }
    public bool CustomerVisibleOnly { get; init; }
    public Guid? CustomerAccountId { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
