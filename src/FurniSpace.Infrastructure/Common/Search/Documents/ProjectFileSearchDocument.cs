namespace FurniSpace.Infrastructure.Common.Search.Documents;

public sealed class ProjectFileSearchDocument
{
    public Guid FileId { get; set; }
    public Guid? FileLinkId { get; set; }
    public Guid ProjectId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public string? Visibility { get; set; }
    public string? MimeType { get; set; }
    public DateTime? UploadedAt { get; set; }
    public Guid? UploadedBy { get; set; }
}
