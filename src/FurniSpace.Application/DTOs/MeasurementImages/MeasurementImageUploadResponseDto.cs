using FurniSpace.Application.DTOs.ProjectFiles;

namespace FurniSpace.Application.DTOs.MeasurementImages;

public sealed class MeasurementImageUploadResponseDto
{
    public ProjectFileUploadResponseDto File { get; set; } = new();
    public Guid ScheduleId { get; set; }
    public MeasurementImageAreaLinkResponseDto? AreaLink { get; set; }
}
