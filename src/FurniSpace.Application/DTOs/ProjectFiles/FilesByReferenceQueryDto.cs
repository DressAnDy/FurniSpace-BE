using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class FilesByReferenceQueryDto
{
    public string ReferenceType { get; init; } = string.Empty;
    public Guid ReferenceId { get; init; }
    public FileType? FileType { get; init; }
    public FileVisibility? Visibility { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
