using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class ArchiveFileResponseDto
{
    public Guid FileId { get; set; }
    public FileStatus Status { get; set; }
    public DateTime ArchivedAt { get; set; }
}
