using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectMeasurementGate
{
    internal static async Task<Error?> ValidateCompletedMeasurementAsync(
        Guid projectId,
        IProjectScheduleRepository scheduleRepository,
        CancellationToken cancellationToken)
    {
        var hasCompleted = await scheduleRepository.HasCompletedMeasurementScheduleAsync(
            projectId,
            cancellationToken);
        if (!hasCompleted)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.MeasurementNotCompleted,
                "A completed measurement schedule is required before proceeding.");
        }

        return null;
    }

    internal static async Task<Error?> ValidateMeasurementFilesAsync(
        Guid projectId,
        IProjectFileRepository fileRepository,
        CancellationToken cancellationToken)
    {
        var hasFiles = await fileRepository.HasProjectFileWithTypesAsync(
            projectId,
            ProjectMeasurementFileTypes.All,
            cancellationToken);
        if (!hasFiles)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.MeasurementFileRequired,
                "At least one measurement-related file is required.");
        }

        return null;
    }

    internal static bool CanMoveToProposalConsulting(Project project, bool hasCompletedMeasurement)
    {
        if (!project.AssignedDesignerId.HasValue)
        {
            return false;
        }

        return project.Status == ProjectStatus.SPACE_VERIFIED || hasCompletedMeasurement;
    }
}
