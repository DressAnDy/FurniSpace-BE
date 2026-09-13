using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Projects;

public static class ProjectMeasurementFileTypes
{
    public static readonly IReadOnlyCollection<FileType> All =
    [
        FileType.FLOOR_PLAN,
        FileType.MEASUREMENT_REPORT,
        FileType.LIDAR_SCAN,
        FileType.SPACE_IMAGE,
        FileType.REFERENCE_IMAGE
    ];
}
