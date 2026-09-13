namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class ProjectChatListResponseDto
{
    public IReadOnlyList<ProjectChatListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
