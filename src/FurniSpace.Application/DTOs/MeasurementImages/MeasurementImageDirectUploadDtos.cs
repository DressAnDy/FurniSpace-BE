using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.MeasurementImages;

public sealed class PrepareMeasurementImageUploadRequestDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public FileVisibility? Visibility { get; set; }
    public string? Note { get; set; }
    public Guid? ProjectAreaId { get; set; }
}

public sealed class PrepareMeasurementImageUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid ProjectId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteMeasurementImageUploadRequestDto
{
    public Guid FileId { get; set; }
}
