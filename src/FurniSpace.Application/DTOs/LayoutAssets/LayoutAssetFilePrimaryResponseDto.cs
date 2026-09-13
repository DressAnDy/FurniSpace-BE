using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class LayoutAssetFilePrimaryResponseDto
{
    public Guid LayoutAssetId { get; set; }
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public FileType? FileType { get; set; }
    public bool IsPrimary { get; set; }
}
