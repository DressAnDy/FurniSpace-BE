using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectChats;

internal static class ProjectChatServiceConstants
{
    internal const int MaxTitleLength = 150;

    internal static readonly ProjectChatType[] CustomerAndSalesChatTypes =
        [ProjectChatType.SALES, ProjectChatType.DESIGNER];

    internal static readonly ProjectChatType[] SalesStaffChatTypes =
        [ProjectChatType.SALES, ProjectChatType.DESIGNER, ProjectChatType.DESIGNER_SALES, ProjectChatType.PRODUCTION];

    internal static readonly ProjectChatType[] ProductionStaffChatTypes = [ProjectChatType.PRODUCTION];

    internal static readonly ProjectChatType[] DesignerChatTypes =
        [ProjectChatType.DESIGNER, ProjectChatType.DESIGNER_SALES];

    internal const string ProductionChatTitle = "Production Coordination";
    internal const string DesignerSalesCoordinationChatTitle = "Designer - Sales Coordination";
}
