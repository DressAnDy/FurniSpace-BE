namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class ProjectFileSearchItemDto
{
    public Guid FileId { get; set; }

    public Guid ProjectId { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public Guid ReferenceId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string? FileType { get; set; }

    public string? Visibility { get; set; }

    public string? MimeType { get; set; }

    public DateTime? UploadedAt { get; set; }
}

public sealed class ProjectFileSearchResponseDto
{
    public IReadOnlyList<ProjectFileSearchItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}
