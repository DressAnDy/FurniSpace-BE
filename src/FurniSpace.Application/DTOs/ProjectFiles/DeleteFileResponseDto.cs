namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class DeleteFileResponseDto
{
    public Guid FileId { get; set; }
    public DateTime DeletedAt { get; set; }
}
