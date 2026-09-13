using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class CreateProjectChatRequestDto
{
    public ProjectChatType ChatType { get; set; }
    public Guid StaffId { get; set; }
    public string Title { get; set; } = string.Empty;
}
