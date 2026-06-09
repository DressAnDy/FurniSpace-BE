namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class FilesByReferenceResponseDto
{
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public IReadOnlyList<FileListItemDto> Items { get; set; } = Array.Empty<FileListItemDto>();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
