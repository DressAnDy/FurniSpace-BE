namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class ProjectChatMessageSearchItemDto
{
    public Guid MessageId { get; set; }

    public Guid ChatId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? SenderId { get; set; }

    public string? SenderName { get; set; }

    public string? MessageType { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
}

public sealed class ProjectChatMessageSearchResponseDto
{
    public IReadOnlyList<ProjectChatMessageSearchItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}
