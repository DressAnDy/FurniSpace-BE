using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Interfaces.Projects;

public interface IProjectPhaseDeadlineService
{
    Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
        Guid projectId,
        Guid currentUserId,
        UpsertProjectPhaseDeadlinesRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task MarkStartedOnceAsync(
        Guid projectId,
        ProjectPhaseType phase,
        DateTime startedAt,
        CancellationToken cancellationToken = default);

    Task MarkCompletedOnceAsync(
        Guid projectId,
        ProjectPhaseType phase,
        DateTime completedAt,
        CancellationToken cancellationToken = default);
}
