namespace FurniSpace.Infrastructure.ReadModels.ProjectFiles;

public sealed class MeasurementImageGalleryQueryReadModel
{
    public Guid? ProjectId { get; init; }
    public Guid? ScheduleId { get; init; }
    public Guid? ProjectAreaId { get; init; }
    public bool? Assigned { get; init; }
    public bool CustomerVisibleOnly { get; init; }
    public Guid? CustomerAccountId { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}

public sealed class MeasurementImageGalleryPageReadModel
{
    public IReadOnlyList<MeasurementImageGalleryItemReadModel> Items { get; init; } = [];
    public int Total { get; init; }
}

public sealed class MeasurementImageGalleryItemReadModel
{
    public Guid FileId { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; }
    public Guid ScheduleId { get; init; }
    public DateTime ScheduledStart { get; init; }
    public IReadOnlyList<MeasurementImageAreaAssignmentReadModel> Areas { get; init; } = [];
}

public sealed class MeasurementImageAreaAssignmentReadModel
{
    public Guid ProjectAreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
}
