namespace FurniSpace.Infrastructure.ReadModels.ProjectChats;

public sealed class ProjectChatAccessReadModel
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? RoleName { get; set; }
}
