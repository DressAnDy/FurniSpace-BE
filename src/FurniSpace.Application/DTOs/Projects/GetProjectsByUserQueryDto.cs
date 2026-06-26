using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class GetProjectsByUserQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ProjectStatus? Status { get; set; }
    public string? RoleScope { get; set; }
    public string? Keyword { get; set; }
}
