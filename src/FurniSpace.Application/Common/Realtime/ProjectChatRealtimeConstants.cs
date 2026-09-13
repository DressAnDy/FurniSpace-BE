namespace FurniSpace.Application.Common.Realtime;

public static class ProjectChatRealtimeConstants
{
    public const string HubPath = "/hubs/project-chat";
    public const string ChatCreatedEvent = "project_chat.created";
    public const string ChatUpdatedEvent = "project_chat.updated";
    public const string MessageSentEvent = "project_chat.message_sent";
    public const string StatusChangedEvent = "project_chat.status_changed";

    public static string Project(Guid projectId) => $"project:{projectId:D}";

    public static string Chat(Guid chatId) => $"project_chat:{chatId:D}";
}
