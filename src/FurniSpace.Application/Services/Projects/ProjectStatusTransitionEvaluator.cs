using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Projects;
using static FurniSpace.Application.Constants.Projects.ProjectStatusTransitionEvaluatorConstants;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectStatusTransitionEvaluator
{
    private readonly IProjectScheduleRepository _schedules;
    private readonly IProjectFileRepository _files;
    private readonly IProposalRepository _proposals;
    private readonly ProjectWorkflowSettings _settings;

    public ProjectStatusTransitionEvaluator(
        IProjectScheduleRepository schedules,
        IProjectFileRepository files,
        IProposalRepository proposals,
        IOptions<ProjectWorkflowSettings> settings)
    {
        _schedules = schedules;
        _files = files;
        _proposals = proposals;
        _settings = settings.Value;
    }

    public async Task<Error?> EvaluateAsync(
        Project project,
        ProjectStatus targetStatus,
        string? note,
        Guid currentUserId,
        string? roleName,
        CancellationToken cancellationToken)
    {
        if (IsCustomer(roleName))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.Forbidden,
                "Customers cannot update project status.");
        }

        var designerAuthorizationError = ValidateDesignerAuthorization(
            project,
            roleName,
            currentUserId,
            targetStatus);
        if (designerAuthorizationError is not null)
        {
            return designerAuthorizationError;
        }

        var noteError = ValidateNoteLength(note);
        if (noteError is not null)
        {
            return noteError;
        }

        var currentStatus = project.Status;
        if (!currentStatus.HasValue)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Project has no current status.");
        }

        if (currentStatus.Value == targetStatus)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Target status must be different from current status.");
        }

        if (targetStatus == ProjectStatus.PROPOSAL_SELECTED &&
            currentStatus.Value != ProjectStatus.PROPOSAL_CONSULTING)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatus,
                "Project must be in proposal consulting before moving to proposal selected.");
        }

        return (currentStatus.Value, targetStatus) switch
        {
            (ProjectStatus.IN_CONSULTATION, ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT) =>
                ValidateInConsultationToWaitingForDesigner(project, roleName, currentUserId),

            (ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, ProjectStatus.MEASUREMENT_REQUIRED) =>
                ValidateWaitingForDesignerToMeasurement(project, roleName, currentUserId),

            (ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, ProjectStatus.SPACE_VERIFIED) =>
                ValidateWaitingForDesignerToMeasurement(project, roleName, currentUserId),

            (ProjectStatus.SPACE_VERIFIED, ProjectStatus.MEASUREMENT_REQUIRED) =>
                ValidateSpaceVerifiedToMeasurementRequired(project, roleName, currentUserId, note),

            (ProjectStatus.MEASUREMENT_REQUIRED, ProjectStatus.SPACE_VERIFIED) =>
                ValidateMeasurementRequiredToSpaceVerified(project, roleName, currentUserId, note),

            (ProjectStatus.MEASUREMENT_REQUIRED, ProjectStatus.PROPOSAL_CONSULTING) =>
                await ValidateToProposalConsultingAsync(project, roleName, currentUserId, cancellationToken),

            (ProjectStatus.SPACE_VERIFIED, ProjectStatus.PROPOSAL_CONSULTING) =>
                await ValidateToProposalConsultingAsync(project, roleName, currentUserId, cancellationToken),

            (ProjectStatus.PROPOSAL_CONSULTING, ProjectStatus.PROPOSAL_SELECTED) =>
                await ValidateProposalConsultingToProposalSelectedAsync(project, roleName, currentUserId, cancellationToken),

            _ => Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                $"Transition from {currentStatus} to {targetStatus} is not supported.")
        };
    }

    private static Error? ValidateInConsultationToWaitingForDesigner(
        Project project,
        string? roleName,
        Guid currentUserId)
    {
        if (!CanActAsSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Only assigned Sales or Admin can update this project status.");
        }

        var missingInformation = GetMissingBasicInformation(project);
        if (missingInformation.Count > 0)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Project basic information is incomplete.");
        }

        return null;
    }

    private static Error? ValidateWaitingForDesignerToMeasurement(
        Project project,
        string? roleName,
        Guid currentUserId)
    {
        if (!CanActAsSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Only assigned Sales or Admin can update this project status.");
        }

        if (!project.AssignedDesignerId.HasValue)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.DesignerNotAssigned,
                "A designer must be assigned before updating measurement status.");
        }

        return null;
    }

    private static Error? ValidateSpaceVerifiedToMeasurementRequired(
        Project project,
        string? roleName,
        Guid currentUserId,
        string? note)
    {
        if (!CanActAsDesignerOrSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Only assigned Designer, Sales, or Admin can update this project status.");
        }

        if (!project.AssignedDesignerId.HasValue)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.DesignerNotAssigned,
                "A designer must be assigned before requiring measurement.");
        }

        return RequireNote(note);
    }

    private static Error? ValidateMeasurementRequiredToSpaceVerified(
        Project project,
        string? roleName,
        Guid currentUserId,
        string? note)
    {
        if (!CanActAsDesignerOrSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.Forbidden,
                "Only assigned Designer, Sales, or Admin can verify project space.");
        }

        return RequireNote(note);
    }

    private static Error? ValidateDesignerAuthorization(
        Project project,
        string? roleName,
        Guid currentUserId,
        ProjectStatus targetStatus)
    {
        if (!IsDesigner(roleName))
        {
            return null;
        }

        if (project.AssignedDesignerId != currentUserId)
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.Forbidden,
                "You do not have access to update this project status.");
        }

        if (DesignerForbiddenTargetStatuses.Contains(targetStatus))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.InvalidProjectStatus,
                "Designer cannot move project to this status.");
        }

        if (!DesignerAllowedTargetStatuses.Contains(targetStatus))
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatus,
                "Designer can only update design-related project statuses.");
        }

        return null;
    }

    private async Task<Error?> ValidateToProposalConsultingAsync(
        Project project,
        string? roleName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (!CanActAsDesignerOrSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.Forbidden,
                "Only assigned Designer, Sales, or Admin can move to proposal consulting.");
        }

        if (!project.AssignedDesignerId.HasValue)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.DesignerNotAssigned,
                "A designer must be assigned before proposal consulting.");
        }

        if (project.Status == ProjectStatus.MEASUREMENT_REQUIRED)
        {
            var measurementError = await ProjectMeasurementGate.ValidateCompletedMeasurementAsync(
                project.ProjectId,
                _schedules,
                cancellationToken);
            if (measurementError is not null)
            {
                return measurementError;
            }

            if (_settings.RequireMeasurementFileOnProposalConsulting)
            {
                var fileError = await ProjectMeasurementGate.ValidateMeasurementFilesAsync(
                    project.ProjectId,
                    _files,
                    cancellationToken);
                if (fileError is not null)
                {
                    return fileError;
                }
            }
        }

        return null;
    }

    private async Task<Error?> ValidateProposalConsultingToProposalSelectedAsync(
        Project project,
        string? roleName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (!CanActAsSales(project, roleName, currentUserId))
        {
            return Error.Forbidden(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Only assigned Sales or Admin can mark a proposal as selected.");
        }

        var hasSelected = await _proposals.HasSelectedFinalProposalAsync(
            project.ProjectId,
            cancellationToken);
        if (!hasSelected)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.FinalProposalRequired,
                "A final proposal must be selected before proceeding.");
        }

        return null;
    }

    private static Error? RequireNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return Error.Validation(
                ProjectStatusErrorCodes.NoteRequired,
                "A note is required for this status transition.");
        }

        return null;
    }

    private static Error? ValidateNoteLength(string? note)
    {
        var normalized = NormalizeOptional(note);
        if (normalized?.Length > MaxNoteLength)
        {
            return Error.Validation(
                ProjectStatusErrorCodes.InvalidProjectStatusTransition,
                "Status update note must not exceed 1000 characters.");
        }

        return null;
    }

    private static List<string> GetMissingBasicInformation(Project project)
    {
        var missing = new List<string>();
        AddMissingField(missing, project.ProjectName, "Project name is required.");
        AddMissingField(missing, project.BusinessType, "Business type is required.");
        AddMissingField(missing, project.FurnitureRequirement, "Furniture requirement is required.");
        return missing;
    }

    private static void AddMissingField(List<string> missing, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(message);
        }
    }

    private static bool CanActAsSales(Project project, string? roleName, Guid currentUserId)
    {
        return IsAdmin(roleName) ||
            (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase) &&
                project.AssignedSalesId == currentUserId);
    }

    private static bool CanActAsDesignerOrSales(Project project, string? roleName, Guid currentUserId)
    {
        return IsAdmin(roleName) ||
            CanActAsSales(project, roleName, currentUserId) ||
            (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase) &&
                project.AssignedDesignerId == currentUserId);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, CustomerRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
