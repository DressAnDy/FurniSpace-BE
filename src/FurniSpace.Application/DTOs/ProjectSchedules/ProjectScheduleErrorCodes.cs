namespace FurniSpace.Application.DTOs.ProjectSchedules;

public static class ProjectScheduleErrorCodes
{
    public const string InvalidScheduleStatus = "INVALID_SCHEDULE_STATUS_TRANSITION";
    public const string InvalidScheduleType = "INVALID_SCHEDULE_TYPE";
    public const string ScheduleTimeInvalid = "SCHEDULE_TIME_INVALID";
    public const string ScheduleOutsideBusinessHours = "SCHEDULE_OUTSIDE_BUSINESS_HOURS";
    public const string ScheduleOverlap = "SCHEDULE_OVERLAP";
    public const string ScheduleMinimumGapNotMet = "SCHEDULE_MINIMUM_GAP_NOT_MET";
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
    public const string SalesCannotCreateDeliverySchedule = "SALES_CANNOT_CREATE_DELIVERY_SCHEDULE";
    public const string NoRemainingDeliveryQuantity = "NO_REMAINING_DELIVERY_QUANTITY";
    public const string ProductionNotCompletedForDeliverySchedule = "PRODUCTION_NOT_COMPLETED_FOR_DELIVERY_SCHEDULE";
    public const string DeliveryScheduleRequiresCompletedBatch = "DELIVERY_SCHEDULE_REQUIRES_COMPLETED_BATCH";
    public const string DeliveryInProgressBlocksScheduleCancel = "DELIVERY_IN_PROGRESS_BLOCKS_SCHEDULE_CANCEL";
    public const string DeliveryScheduleLocationFrozen = "DELIVERY_SCHEDULE_LOCATION_FROZEN";
}
