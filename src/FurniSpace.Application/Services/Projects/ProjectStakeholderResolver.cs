using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectStakeholderResolver : IProjectStakeholderResolver
{
    private readonly IProjectRepository _projects;

    public ProjectStakeholderResolver(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<ProjectStakeholders?> ResolveAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        return new ProjectStakeholders
        {
            CustomerId = project.CustomerId,
            AssignedSalesId = project.AssignedSalesId,
            AssignedDesignerId = project.AssignedDesignerId
        };
    }
}
