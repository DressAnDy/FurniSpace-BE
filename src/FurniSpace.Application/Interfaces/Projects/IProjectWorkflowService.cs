#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;

namespace FurniSpace.Application.Interfaces.Projects;

public interface IProjectWorkflowService
{
    Task<ServiceResult<ProjectWorkflowDto>> GetWorkflowAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
