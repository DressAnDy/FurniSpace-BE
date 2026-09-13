using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectShowcases;

public sealed class PrepareProjectShowcaseMediaUploadRequestDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
}

public sealed class PrepareProjectShowcaseMediaUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid ShowcaseId { get; set; }
    public Guid ProjectId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteProjectShowcaseMediaUploadRequestDto
{
    public Guid FileId { get; set; }
    public ProjectShowcaseMediaType MediaType { get; set; } = ProjectShowcaseMediaType.FINAL;
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool SetAsCover { get; set; }
}
