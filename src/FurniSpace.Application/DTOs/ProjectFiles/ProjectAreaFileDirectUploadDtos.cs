namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class PrepareProjectAreaFileUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid ProjectAreaId { get; set; }
    public Guid ProjectId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
