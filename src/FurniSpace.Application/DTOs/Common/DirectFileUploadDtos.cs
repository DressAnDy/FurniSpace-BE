namespace FurniSpace.Application.DTOs.Common;

public sealed class PrepareDirectUploadResponseDto
{
    public Guid FileId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteDirectUploadRequestDto
{
    public Guid FileId { get; set; }
}
