using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class SendTextChatMessageRequestDto
{
    public ProjectChatMessageType MessageType { get; set; } = ProjectChatMessageType.TEXT;
    public string Content { get; set; } = string.Empty;
}
