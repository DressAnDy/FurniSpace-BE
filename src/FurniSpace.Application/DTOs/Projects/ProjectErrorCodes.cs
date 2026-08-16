namespace FurniSpace.Application.DTOs.Projects;

public static class ProjectErrorCodes
{
    public const string InvalidTargetCompletionDate = "INVALID_TARGET_COMPLETION_DATE";
    public const string TargetDateConflictsWithOperationalDates = "TARGET_DATE_CONFLICTS_WITH_OPERATIONAL_DATES";
    public const string TargetDateConflictsWithActiveStartFee = "TARGET_DATE_CONFLICTS_WITH_ACTIVE_START_FEE";
    public const string TargetCompletionDateNotEditable = "TARGET_COMPLETION_DATE_NOT_EDITABLE";
    public const string ProjectNotDelivered = "PROJECT_NOT_DELIVERED";
    public const string RelatedOrderNotCompleted = "RELATED_ORDER_NOT_COMPLETED";
    public const string RelatedOrderNotFound = "RELATED_ORDER_NOT_FOUND";
    public const string DeliveryNotConfirmed = "DELIVERY_NOT_CONFIRMED";
}
