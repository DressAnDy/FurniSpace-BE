using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.MeasurementImages;

public sealed class UploadMeasurementImageRequestDto
{
    public Stream Content { get; set; } = Stream.Null;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public FileVisibility? Visibility { get; set; }
    public string? Note { get; set; }
    public Guid? ProjectAreaId { get; set; }
}
