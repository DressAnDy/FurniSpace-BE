using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.MeasurementImages;

public sealed class RegisterMeasurementImageRequestDto
{
    public string StoragePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public FileVisibility? Visibility { get; set; }
    public string? Note { get; set; }
}
