using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectSchedules;

internal static class ProjectScheduleServiceConstants
{
    internal const string ScheduleNotFoundMessage = "Schedule not found.";
    internal const string ProjectScheduleReferenceType = "PROJECT_SCHEDULE";

    internal static readonly ProjectScheduleType[] ProductionManageableScheduleTypes =
    [
        ProjectScheduleType.DELIVERY,
        ProjectScheduleType.HANDOVER,
        ProjectScheduleType.OTHER
    ];

    internal static readonly ProjectScheduleType[] ProductionStatusScheduleTypes =
    [
        ProjectScheduleType.DELIVERY,
        ProjectScheduleType.HANDOVER
    ];

    internal static readonly OrderStatus[] DeliveryReadyOrderStatuses =
    [
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING
    ];
}
