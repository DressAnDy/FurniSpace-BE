using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class RoomPlannerAreaBlueprintDto
{
    public Guid ProjectAreaId { get; set; }
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public FileType? FileType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
