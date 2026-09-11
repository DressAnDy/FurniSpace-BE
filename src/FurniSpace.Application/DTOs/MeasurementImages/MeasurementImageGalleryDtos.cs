namespace FurniSpace.Application.DTOs.MeasurementImages;

public sealed class MeasurementImageGalleryQueryDto
{
    public Guid? ScheduleId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public bool? Assigned { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public sealed class MeasurementImageGalleryResponseDto
{
    public IReadOnlyList<MeasurementImageGalleryItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}

public sealed class MeasurementImageGalleryItemDto
{
    public Guid FileId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public MeasurementImageScheduleSummaryDto MeasurementSchedule { get; set; } = new();
    public IReadOnlyList<MeasurementImageAreaSummaryDto> Areas { get; set; } = [];
}

public sealed class MeasurementImageScheduleSummaryDto
{
    public Guid ScheduleId { get; set; }
    public DateTime ScheduledStart { get; set; }
}

public sealed class MeasurementImageAreaSummaryDto
{
    public Guid ProjectAreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
}

public sealed class MeasurementImageAreaLinkResponseDto
{
    public Guid ProjectAreaId { get; set; }
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
}
