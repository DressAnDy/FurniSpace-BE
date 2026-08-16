using FurniSpace.Application.Common.Projects;

namespace FurniSpace.Application.Interfaces.Projects;

public interface IProjectStakeholderResolver
{
    Task<ProjectStakeholders?> ResolveAsync(Guid projectId, CancellationToken cancellationToken = default);
}
