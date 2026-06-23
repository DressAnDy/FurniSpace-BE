using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectChats;

public sealed class ProjectChatListQueryReadModel
{
    public ProjectChatStatus? Status { get; set; }
    public ProjectChatType? ChatType { get; set; }
    public IReadOnlyCollection<ProjectChatType>? AllowedChatTypes { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
