using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.Projects;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectPhaseDeadlineService : IProjectPhaseDeadlineService
{
    private const string PhaseDeadlineNotPlannedMessage = "Project phase deadlines have not been planned.";
    private const string ManageForbiddenMessage = "You do not have access to manage project phase deadlines.";
    private const string ViewForbiddenMessage = "You do not have access to view project phase deadlines.";
    private const string PlannedStatus = "PLANNED";
    private const string OnTrackStatus = "ON_TRACK";
    private const string OverdueStatus = "OVERDUE";
    private const string CompletedOnTimeStatus = "COMPLETED_ON_TIME";
    private const string CompletedLateStatus = "COMPLETED_LATE";

    private static readonly ProjectPhaseType[] SupportedPhases =
    [
        ProjectPhaseType.PROPOSAL,
        ProjectPhaseType.PRODUCTION
    ];

    private readonly IProjectRepository _projects;
    private readonly IProjectPhaseDeadlineRepository _deadlines;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectPhaseDeadlineService(
        IProjectRepository projects,
        IProjectPhaseDeadlineRepository deadlines,
        IProductionRequestRepository productionRequests,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _deadlines = deadlines;
        _productionRequests = productionRequests;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
        Guid projectId,
        Guid currentUserId,
        UpsertProjectPhaseDeadlinesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var requestError = ValidateIdentity<ProjectPhaseDeadlinePlanDto>(projectId, currentUserId);
        if (requestError is not null)
        {
            return requestError;
        }

        var dateValidationError = ValidateDeadlineRequest(request);
        if (dateValidationError is not null)
        {
            return dateValidationError;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.NotFound(ProjectServiceConstants.ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManage(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.Forbidden(ManageForbiddenMessage);
        }

        if (project.Status != ProjectStatus.IN_CONSULTATION)
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.Failure(Error.BadRequest(
                "INVALID_PROJECT_STATUS",
                "Project phase deadlines can only be planned while project status is IN_CONSULTATION."));
        }

        var timelineError = ValidateTimeline(request, project.TargetCompletionDate);
        if (timelineError is not null)
        {
            return timelineError;
        }

        var now = DateTime.UtcNow;
        var existing = await _deadlines.GetByProjectAsync(projectId, cancellationToken);
        await UpsertDeadlineAsync(
            existing,
            projectId,
            ProjectPhaseType.PROPOSAL,
            request.ProposalDueDate!.Value,
            currentUserId,
            now,
            cancellationToken);
        await UpsertDeadlineAsync(
            existing,
            projectId,
            ProjectPhaseType.PRODUCTION,
            request.ProductionDueDate!.Value,
            currentUserId,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var deadlines = await _deadlines.GetByProjectAsync(projectId, cancellationToken);
        return ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(
            ToPlanDto(project, deadlines, DateOnly.FromDateTime(now)),
            "Project phase deadlines saved successfully.");
    }

    public async Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var requestError = ValidateIdentity<ProjectPhaseDeadlinePlanDto>(projectId, currentUserId);
        if (requestError is not null)
        {
            return requestError;
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.NotFound(ProjectServiceConstants.ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewAsync(project, currentUserId, roleName, cancellationToken))
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.Forbidden(ViewForbiddenMessage);
        }

        var deadlines = await _deadlines.GetByProjectAsync(projectId, cancellationToken);
        return ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(
            ToPlanDto(project, deadlines, DateOnly.FromDateTime(DateTime.UtcNow)),
            deadlines.Count == 0 ? PhaseDeadlineNotPlannedMessage : "Project phase deadlines retrieved successfully.");
    }

    public async Task MarkCompletedOnceAsync(
        Guid projectId,
        ProjectPhaseType phase,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedPhases.Contains(phase))
        {
            return;
        }

        var deadline = await _deadlines.GetByProjectAndPhaseAsync(projectId, phase, cancellationToken);
        if (deadline is null || deadline.CompletedAt.HasValue)
        {
            return;
        }

        deadline.CompletedAt = completedAt;
        deadline.UpdatedAt = completedAt;
        _deadlines.Update(deadline);
    }

    private static ServiceResult<T>? ValidateIdentity<T>(Guid projectId, Guid currentUserId)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<T>.BadRequest(ProjectServiceConstants.ProjectIdRequiredMessage);
        }

        return currentUserId == Guid.Empty
            ? ServiceResult<T>.Unauthorized(ProjectServiceConstants.AuthenticatedAccountIdRequiredMessage)
            : null;
    }

    private static ServiceResult<ProjectPhaseDeadlinePlanDto>? ValidateDeadlineRequest(
        UpsertProjectPhaseDeadlinesRequestDto request)
    {
        var errors = new List<string>();
        if (!request.ProposalDueDate.HasValue)
        {
            errors.Add("Proposal due date is required.");
        }

        if (!request.ProductionDueDate.HasValue)
        {
            errors.Add("Production due date is required.");
        }

        return errors.Count == 0
            ? null
            : ServiceResult<ProjectPhaseDeadlinePlanDto>.BadRequest(errors);
    }

    private static ServiceResult<ProjectPhaseDeadlinePlanDto>? ValidateTimeline(
        UpsertProjectPhaseDeadlinesRequestDto request,
        DateOnly? targetCompletionDate)
    {
        if (request.ProposalDueDate > request.ProductionDueDate)
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.Failure(Error.BadRequest(
                "INVALID_PHASE_DEADLINE_RANGE",
                "Proposal due date must be on or before production due date."));
        }

        return targetCompletionDate.HasValue && request.ProductionDueDate > targetCompletionDate
            ? ServiceResult<ProjectPhaseDeadlinePlanDto>.Failure(Error.BadRequest(
                "PHASE_DEADLINE_EXCEEDS_TARGET",
                "Production due date must be on or before project target completion date."))
            : null;
    }

    private async Task UpsertDeadlineAsync(
        List<ProjectPhaseDeadline> existing,
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate,
        Guid currentUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var deadline = existing.FirstOrDefault(item => item.Phase == phase);
        if (deadline is null)
        {
            await _deadlines.AddAsync(
                new ProjectPhaseDeadline
                {
                    ProjectPhaseDeadlineId = Guid.NewGuid(),
                    ProjectId = projectId,
                    Phase = phase,
                    DueDate = dueDate,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                cancellationToken);
            return;
        }

        deadline.DueDate = dueDate;
        deadline.UpdatedBy = currentUserId;
        deadline.UpdatedAt = now;
        _deadlines.Update(deadline);
    }

    private async Task<bool> CanViewAsync(
        ProjectDetailReadModel project,
        Guid currentUserId,
        string? roleName,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return project.CustomerId == currentUserId;
        }

        if (IsSales(roleName))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (IsDesigner(roleName))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return IsProduction(roleName) &&
            await _productionRequests.HasViewableAssignedRequestAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken);
    }

    private static bool CanManage(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (IsSales(roleName) && project.AssignedSalesId == currentUserId);
    }

    private static ProjectPhaseDeadlinePlanDto ToPlanDto(
        Project project,
        List<ProjectPhaseDeadline> deadlines,
        DateOnly today)
    {
        return ToPlanDto(
            new ProjectDetailReadModel
            {
                ProjectId = project.ProjectId,
                TargetCompletionDate = project.TargetCompletionDate
            },
            deadlines,
            today);
    }

    private static ProjectPhaseDeadlinePlanDto ToPlanDto(
        ProjectDetailReadModel project,
        List<ProjectPhaseDeadline> deadlines,
        DateOnly today)
    {
        var orderedDeadlines = deadlines
            .Where(deadline => SupportedPhases.Contains(deadline.Phase))
            .OrderBy(deadline => deadline.DueDate)
            .ThenBy(deadline => deadline.Phase)
            .ToList();
        var firstOpenDeadlineId = orderedDeadlines
            .FirstOrDefault(deadline => deadline.CompletedAt is null)
            ?.ProjectPhaseDeadlineId;

        return new ProjectPhaseDeadlinePlanDto
        {
            ProjectId = project.ProjectId,
            TargetCompletionDate = project.TargetCompletionDate,
            Deadlines = orderedDeadlines
                .Select(deadline => ToDeadlineDto(deadline, today, firstOpenDeadlineId))
                .ToList()
        };
    }

    private static ProjectPhaseDeadlineItemDto ToDeadlineDto(
        ProjectPhaseDeadline deadline,
        DateOnly today,
        Guid? firstOpenDeadlineId)
    {
        var dueDate = deadline.DueDate;
        var completionDate = deadline.CompletedAt.HasValue
            ? DateOnly.FromDateTime(deadline.CompletedAt.Value)
            : (DateOnly?)null;
        var overdueDays = CalculateOverdueDays(dueDate, completionDate ?? today);

        return new ProjectPhaseDeadlineItemDto
        {
            Phase = deadline.Phase,
            DueDate = dueDate,
            CompletedAt = deadline.CompletedAt,
            Status = ResolveStatus(deadline, today, completionDate, firstOpenDeadlineId),
            OverdueDays = overdueDays
        };
    }

    private static int CalculateOverdueDays(DateOnly dueDate, DateOnly comparisonDate)
    {
        return Math.Max(0, comparisonDate.DayNumber - dueDate.DayNumber);
    }

    private static string ResolveStatus(
        ProjectPhaseDeadline deadline,
        DateOnly today,
        DateOnly? completionDate,
        Guid? firstOpenDeadlineId)
    {
        if (completionDate.HasValue)
        {
            return completionDate.Value > deadline.DueDate ? CompletedLateStatus : CompletedOnTimeStatus;
        }

        if (today > deadline.DueDate)
        {
            return OverdueStatus;
        }

        return deadline.ProjectPhaseDeadlineId == firstOpenDeadlineId ? OnTrackStatus : PlannedStatus;
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProduction(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Production, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSales(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);
    }
}
