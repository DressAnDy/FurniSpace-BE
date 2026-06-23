using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class ProjectChatListQueryDto
{
    public ProjectChatStatus? Status { get; set; }
    public ProjectChatType? ChatType { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
