namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class ProjectChatListItemDto
{
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public string ChatType { get; set; } = string.Empty;
    public Guid? StaffId { get; set; }
    public string? StaffName { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public ProjectChatLastMessageDto? LastMessage { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
