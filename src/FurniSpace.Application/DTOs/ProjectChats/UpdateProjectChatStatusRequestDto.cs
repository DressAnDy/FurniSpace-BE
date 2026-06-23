using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class UpdateProjectChatStatusRequestDto
{
    public ProjectChatStatus? Status { get; set; }
}
