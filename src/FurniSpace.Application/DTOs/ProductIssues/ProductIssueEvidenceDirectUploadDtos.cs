namespace FurniSpace.Application.DTOs.ProductIssues;

public sealed class PrepareProductIssueEvidenceUploadRequestDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
}

public sealed class PrepareProductIssueEvidenceUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid OrderId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteProductIssueEvidenceUploadRequestDto
{
    public Guid FileId { get; set; }
}

public sealed class CompleteProductIssueEvidenceUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public Guid OrderId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}
