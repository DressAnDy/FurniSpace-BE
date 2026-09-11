using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class LayoutAssetFileDto
{
    public Guid FileId { get; set; }
    public FileType? FileType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int? DisplayOrder { get; set; }
    public FileStatus Status { get; set; }
}
