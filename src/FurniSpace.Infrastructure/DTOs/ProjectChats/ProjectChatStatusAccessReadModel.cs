using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectChats;

public sealed class ProjectChatStatusAccessReadModel
{
    public Guid ChatId { get; init; }
    public Guid ProjectId { get; init; }
    public ProjectChatType ChatType { get; init; }
    public ProjectChatStatus? ChatStatus { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public string? RoleName { get; init; }
}
