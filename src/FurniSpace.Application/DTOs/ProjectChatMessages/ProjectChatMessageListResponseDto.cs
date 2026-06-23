namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class ProjectChatMessageListResponseDto
{
    public IReadOnlyList<ProjectChatMessageDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
