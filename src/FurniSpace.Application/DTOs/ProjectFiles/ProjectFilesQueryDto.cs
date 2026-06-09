using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class ProjectFilesQueryDto
{
    public FileType? FileType { get; init; }
    public FileVisibility? Visibility { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
