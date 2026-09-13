namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class ProjectFilesResponseDto
{
    public IReadOnlyList<FileListItemDto> Items { get; set; } = Array.Empty<FileListItemDto>();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
