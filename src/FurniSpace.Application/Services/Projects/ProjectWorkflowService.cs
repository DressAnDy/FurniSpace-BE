#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectWorkflowService : IProjectWorkflowService
{
    private const string ProjectIdRequiredMessage = "Project id is required.";
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string SuccessMessage = "Project workflow retrieved successfully.";

    private readonly IProjectWorkflowRepository _workflows;

    public ProjectWorkflowService(IProjectWorkflowRepository workflows)
    {
        _workflows = workflows;
    }

    public async Task<ServiceResult<ProjectWorkflowDto>> GetWorkflowAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectWorkflowDto>.BadRequest(ProjectIdRequiredMessage);
        }

        var snapshot = await _workflows.GetSnapshotAsync(projectId, cancellationToken);
        if (snapshot is null)
        {
            return ServiceResult<ProjectWorkflowDto>.NotFound(ProjectNotFoundMessage);
        }

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        return ServiceResult<ProjectWorkflowDto>.Success(dto, SuccessMessage);
    }
}
