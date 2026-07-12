using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectChats;

internal static class ProjectChatServiceConstants
{
    internal const string AdminRole = "ADMIN";
    internal const string CustomerRole = "CUSTOMER";
    internal const string DesignerRole = "DESIGNER";
    internal const string SalesRole = "SALES";
    internal const int MaxTitleLength = 150;

    internal static readonly ProjectChatType[] CustomerAndSalesChatTypes =
        [ProjectChatType.SALES, ProjectChatType.DESIGNER];

    internal static readonly ProjectChatType[] DesignerChatTypes = [ProjectChatType.DESIGNER];
}
