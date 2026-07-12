using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectSchedules;

internal static class ProjectScheduleServiceConstants
{
    internal const string AdminRole = "ADMIN";
    internal const string SalesRole = "SALES";
    internal const string CustomerRole = "CUSTOMER";
    internal const string DesignerRole = "DESIGNER";
    internal const string ProductionRole = "PRODUCTION";
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
}
