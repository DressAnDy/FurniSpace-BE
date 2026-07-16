using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectChats;

internal static class ProjectChatServiceConstants
{
    internal const int MaxTitleLength = 150;

    internal static readonly ProjectChatType[] CustomerAndSalesChatTypes =
        [ProjectChatType.SALES, ProjectChatType.DESIGNER];

    internal static readonly ProjectChatType[] DesignerChatTypes = [ProjectChatType.DESIGNER];
}
