namespace FurniSpace.Application.DTOs.MeasurementImages;

public static class MeasurementImageErrorCodes
{
    public const string ScheduleNotEligible = "MEASUREMENT_IMAGE_SCHEDULE_NOT_ELIGIBLE";
    public const string CaptureBeforeStart = "MEASUREMENT_IMAGE_CAPTURE_BEFORE_START";
    public const string InvalidFileMetadata = "MEASUREMENT_IMAGE_INVALID_FILE_METADATA";
    public const string StoragePathInvalid = "MEASUREMENT_IMAGE_STORAGE_PATH_INVALID";
    public const string StoragePathDuplicate = "MEASUREMENT_IMAGE_STORAGE_PATH_DUPLICATE";
    public const string NotMeasurementImage = "MEASUREMENT_IMAGE_NOT_FOUND";
    public const string AreaLinkExists = "MEASUREMENT_IMAGE_AREA_LINK_EXISTS";
    public const string AreaLinkNotFound = "MEASUREMENT_IMAGE_AREA_LINK_NOT_FOUND";
    public const string ScheduleProjectMismatch = "MEASUREMENT_IMAGE_SCHEDULE_PROJECT_MISMATCH";
}
