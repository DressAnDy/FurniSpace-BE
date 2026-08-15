using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;

public sealed class ProjectChatMessageAccessReadModel
{
    public Guid ChatId { get; init; }
    public Guid ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public ProjectChatType ChatType { get; init; }
    public string? ChatTitle { get; init; }
    public Guid? ChatStaffId { get; init; }
    public ProjectChatStatus? ChatStatus { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public string? CurrentUserName { get; init; }
    public string? RoleName { get; init; }
}
