using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class ProjectByUserQueryReadModel
{
    public Guid UserId { get; set; }
    public string RoleScope { get; set; } = string.Empty;
    public ProjectStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
