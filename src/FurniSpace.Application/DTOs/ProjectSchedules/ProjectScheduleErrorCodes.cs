namespace FurniSpace.Application.DTOs.ProjectSchedules;

public static class ProjectScheduleErrorCodes
{
    public const string InvalidScheduleStatus = "INVALID_SCHEDULE_STATUS_TRANSITION";
    public const string InvalidScheduleType = "INVALID_SCHEDULE_TYPE";
    public const string MeasurementFileRequired = "MEASUREMENT_FILE_REQUIRED";
    public const string DesignerNotAssigned = "DESIGNER_NOT_ASSIGNED";
    public const string InvalidProjectStatus = "INVALID_PROJECT_STATUS";
    public const string OrderNotReadyForDelivery = "ORDER_NOT_READY_FOR_DELIVERY";
    public const string InvalidDeliverySchedule = "INVALID_DELIVERY_SCHEDULE";
    public const string ScheduleDateExceedsTarget = "SCHEDULE_DATE_EXCEEDS_TARGET";
    public const string ActiveDeliveryScheduleExists = "ACTIVE_DELIVERY_SCHEDULE_EXISTS";
    public const string DeliveryScheduleNotAllowedAfterCompletion = "DELIVERY_SCHEDULE_NOT_ALLOWED_AFTER_COMPLETION";
    public const string StaffScheduleOverlap = "STAFF_SCHEDULE_OVERLAP";
    public const string ScheduleCompleteBeforeStart = "SCHEDULE_COMPLETE_BEFORE_START";
}
