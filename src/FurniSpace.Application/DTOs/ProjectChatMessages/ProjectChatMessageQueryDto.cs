namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class ProjectChatMessageQueryDto
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 30;
    public string Sort { get; set; } = "ASC";
}
