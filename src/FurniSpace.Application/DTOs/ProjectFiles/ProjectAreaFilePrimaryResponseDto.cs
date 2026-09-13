using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class ProjectAreaFilePrimaryResponseDto
{
    public Guid ProjectAreaId { get; set; }
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public FileType? FileType { get; set; }
    public bool IsPrimary { get; set; }
}
