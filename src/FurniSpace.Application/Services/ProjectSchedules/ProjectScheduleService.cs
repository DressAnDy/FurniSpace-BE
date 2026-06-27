using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.ProjectSchedules;

public sealed class ProjectScheduleService : IProjectScheduleService
{
    private const string AdminRole = "ADMIN";
    private const string SalesRole = "SALES";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string ScheduleNotFoundMessage = "Schedule not found.";
    private const string ProjectScheduleReferenceType = "PROJECT_SCHEDULE";

    private readonly IProjectScheduleRepository _schedules;
    private readonly IProjectRepository _projects;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectScheduleService(
        IProjectScheduleRepository schedules,
        IProjectRepository projects,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _projects = projects;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectScheduleDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleDto>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Project id is required.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (AdminRole or SalesRole))
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("Only assigned Sales or Admin can create schedules.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound("Project not found.");
        }

        if (role == SalesRole && project.AssignedSalesId != currentUserId)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("You are not the assigned Sales for this project.");
        }

        var now = DateTime.UtcNow;
        if (request.ScheduledStart <= now)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Scheduled start time must not be in the past.");
        }

        if (request.ScheduledEnd.HasValue && request.ScheduledEnd.Value <= request.ScheduledStart)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Scheduled end time must be after scheduled start time.");
        }

        var schedule = new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = projectId,
            ScheduleType = request.ScheduleType,
            Title = request.Title,
            Description = request.Description,
            CreatedBy = currentUserId,
            AssignedStaffId = request.AssignedStaffId,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            Location = request.Location,
            Status = ProjectScheduleStatus.PENDING_CONFIRMATION,
            CustomerNote = request.CustomerNote,
            InternalNote = request.InternalNote,
            CreatedAt = now,
            UpdatedAt = now
        };

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _schedules.AddAsync(schedule, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        await DispatchScheduleCreatedAsync(schedule, project, cancellationToken);

        return ServiceResult<ProjectScheduleDto>.Created(
            schedule.Adapt<ProjectScheduleDto>(),
            "Project schedule created successfully.");
    }

    public async Task<ServiceResult<ProjectScheduleListResponseDto>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectScheduleListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleListResponseDto>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleListResponseDto>.BadRequest("Project id is required.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectScheduleListResponseDto>.NotFound("Project not found.");
        }

        if (!CanViewProjectSchedules(role, project, currentUserId))
        {
            return ServiceResult<ProjectScheduleListResponseDto>.Forbidden("You do not have access to this project's schedules.");
        }

        var normalizedQuery = NormalizeQuery(query);
        var (items, total) = await _schedules.GetListByProjectAsync(
            projectId,
            normalizedQuery,
            cancellationToken);

        return ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto
        {
            Items = items.Adapt<IReadOnlyList<ProjectScheduleDto>>(),
            Total = total,
            Page = normalizedQuery.Page,
            Limit = normalizedQuery.Limit
        });
    }

    public async Task<ServiceResult<ProjectScheduleDto>> GetDetailAsync(
        Guid scheduleId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleDto>.Unauthorized();
        }

        var detail = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessSchedule(role, detail, currentUserId))
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("You do not have access to this schedule.");
        }

        return ServiceResult<ProjectScheduleDto>.Success(detail.Adapt<ProjectScheduleDto>());
    }

    public async Task<ServiceResult<ProjectScheduleDto>> UpdateAsync(
        Guid scheduleId,
        Guid currentUserId,
        UpdateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleDto>.Unauthorized();
        }

        var detail = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var authorizationError = ValidateUpdatePermission(role, detail, currentUserId);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var validationError = ValidateUpdateTime(request, detail);
        if (validationError is not null)
        {
            return validationError;
        }

        var timeChanged = ApplyScheduleUpdates(schedule, request, detail);

        if (timeChanged)
        {
            schedule.Status = ProjectScheduleStatus.PENDING_CONFIRMATION;
        }

        schedule.UpdatedAt = DateTime.UtcNow;

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _schedules.Update(schedule);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        var updatedDetail = new ProjectScheduleDetailReadModel
        {
            ScheduleId = schedule.ScheduleId,
            ProjectId = detail.ProjectId,
            ProjectName = detail.ProjectName,
            CustomerId = detail.CustomerId,
            AssignedSalesId = detail.AssignedSalesId,
            AssignedDesignerId = detail.AssignedDesignerId,
            AssignedStaffId = schedule.AssignedStaffId
        };

        await DispatchScheduleUpdatedAsync(schedule, updatedDetail, cancellationToken);

        return ServiceResult<ProjectScheduleDto>.Success(schedule.Adapt<ProjectScheduleDto>());
    }

    public async Task<ServiceResult<ProjectScheduleDto>> UpdateStatusAsync(
        Guid scheduleId,
        Guid currentUserId,
        UpdateProjectScheduleStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleDto>.Unauthorized();
        }

        var detail = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var currentStatus = detail.Status;
        var newStatus = request.Status;

        var transitionError = ValidateStatusTransition(role, currentStatus, newStatus, detail, currentUserId);
        if (transitionError is not null)
        {
            return transitionError;
        }

        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        schedule.Status = newStatus;
        schedule.UpdatedAt = now;

        if (newStatus == ProjectScheduleStatus.CANCELLED)
        {
            schedule.CancelledAt = now;
        }

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _schedules.Update(schedule);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        await DispatchStatusChangedAsync(schedule, detail, newStatus, cancellationToken);

        return ServiceResult<ProjectScheduleDto>.Success(
            schedule.Adapt<ProjectScheduleDto>(),
            $"Schedule status updated to {newStatus}.");
    }

    public async Task<ServiceResult<ProjectScheduleListResponseDto>> GetMyAssignedAsync(
        Guid currentUserId,
        ProjectScheduleListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectScheduleListResponseDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is null)
        {
            return ServiceResult<ProjectScheduleListResponseDto>.Unauthorized();
        }

        Guid? staffId = role == AdminRole ? null : currentUserId;

        var normalizedQuery = NormalizeQuery(query);
        var (items, total) = await _schedules.GetMyAssignedAsync(staffId, normalizedQuery, cancellationToken);

        return ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto
        {
            Items = items.Adapt<IReadOnlyList<ProjectScheduleDto>>(),
            Total = total,
            Page = normalizedQuery.Page,
            Limit = normalizedQuery.Limit
        });
    }

    private static bool CanViewProjectSchedules(
        string? role,
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        Guid currentUserId)
    {
        return role switch
        {
            AdminRole => true,
            CustomerRole => project.CustomerId == currentUserId,
            SalesRole => project.AssignedSalesId == currentUserId,
            DesignerRole => project.AssignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool CanAccessSchedule(
        string? role,
        ProjectScheduleDetailReadModel schedule,
        Guid currentUserId)
    {
        if (role == AdminRole) return true;
        if (schedule.CustomerId == currentUserId) return true;
        if (schedule.AssignedSalesId == currentUserId) return true;
        if (schedule.AssignedDesignerId == currentUserId) return true;
        if (schedule.AssignedStaffId == currentUserId) return true;
        return false;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateStatusTransition(
        string? role,
        ProjectScheduleStatus? currentStatus,
        ProjectScheduleStatus newStatus,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        var terminalStatusError = ValidateTerminalStatus(currentStatus);
        if (terminalStatusError is not null)
        {
            return terminalStatusError;
        }

        switch (newStatus)
        {
            case ProjectScheduleStatus.CONFIRMED:
                return ValidateConfirmTransition(role, currentStatus, detail, currentUserId);
            case ProjectScheduleStatus.COMPLETED:
                return ValidateCompletedTransition(role, currentStatus, detail, currentUserId);
            case ProjectScheduleStatus.CANCELLED:
                return ValidateCancelledTransition(role, currentStatus, detail, currentUserId);
            default:
                return ServiceResult<ProjectScheduleDto>.BadRequest($"Invalid target status: {newStatus}.");
        }
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateMeasurementScheduleCreate(
        string? role,
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        Guid? assignedStaffId)
    {
        if (!project.AssignedDesignerId.HasValue)
        {
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Validation(
                    ProjectScheduleErrorCodes.DesignerNotAssigned,
                    "Project must have an assigned designer before creating a measurement schedule."));
        }

        if (project.Status != ProjectStatus.MEASUREMENT_REQUIRED)
        {
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Validation(
                    ProjectScheduleErrorCodes.InvalidProjectStatus,
                    "Project must be in MEASUREMENT_REQUIRED status to create a measurement schedule."));
        }

        if (!assignedStaffId.HasValue)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Assigned staff id is required.");
        }

        if (role != AdminRole && assignedStaffId.Value != project.AssignedDesignerId.Value)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden(
                "Measurement schedules must be assigned to the project's designer.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateUpdatePermission(
        string? role,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        if (role != AdminRole && !(role == SalesRole && detail.AssignedSalesId == currentUserId))
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("Only assigned Sales or Admin can update schedules.");
        }

        if (role != AdminRole &&
            detail.Status is not (ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED))
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Completed or cancelled schedules cannot be updated.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateUpdateTime(
        UpdateProjectScheduleRequestDto request,
        ProjectScheduleDetailReadModel detail)
    {
        if (request.ScheduledStart.HasValue && request.ScheduledStart.Value <= DateTime.UtcNow)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Scheduled start time must not be in the past.");
        }

        var effectiveStart = request.ScheduledStart ?? detail.ScheduledStart;
        var effectiveEnd = request.ScheduledEnd ?? detail.ScheduledEnd;
        if (effectiveEnd.HasValue && effectiveEnd.Value <= effectiveStart)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Scheduled end time must be after scheduled start time.");
        }

        return null;
    }

    private static bool ApplyScheduleUpdates(
        ProjectSchedule schedule,
        UpdateProjectScheduleRequestDto request,
        ProjectScheduleDetailReadModel detail)
    {
        var timeChanged = request.ScheduledStart.HasValue && request.ScheduledStart.Value != detail.ScheduledStart;

        if (request.Title is not null) schedule.Title = request.Title;
        if (request.Description is not null) schedule.Description = request.Description;
        if (request.AssignedStaffId.HasValue) schedule.AssignedStaffId = request.AssignedStaffId;
        if (request.ScheduledStart.HasValue) schedule.ScheduledStart = request.ScheduledStart.Value;
        if (request.ScheduledEnd.HasValue) schedule.ScheduledEnd = request.ScheduledEnd;
        if (request.Location is not null) schedule.Location = request.Location;
        if (request.CustomerNote is not null) schedule.CustomerNote = request.CustomerNote;
        if (request.InternalNote is not null) schedule.InternalNote = request.InternalNote;

        return timeChanged;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateTerminalStatus(ProjectScheduleStatus? currentStatus)
    {
        if (currentStatus is ProjectScheduleStatus.COMPLETED or ProjectScheduleStatus.CANCELLED)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest(
                $"Cannot transition from terminal status {currentStatus}.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateConfirmTransition(
        string? role,
        ProjectScheduleStatus? currentStatus,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        if (currentStatus != ProjectScheduleStatus.PENDING_CONFIRMATION)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest(
                "Only PENDING_CONFIRMATION schedules can be confirmed.");
        }

        if (role != CustomerRole || detail.CustomerId != currentUserId)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden(
                "Only the customer owner can confirm a schedule.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateCompletedTransition(
        string? role,
        ProjectScheduleStatus? currentStatus,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        if (currentStatus != ProjectScheduleStatus.CONFIRMED)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest(
                "Only CONFIRMED schedules can be completed.");
        }

        var canComplete = role == AdminRole
            || role == SalesRole && detail.AssignedSalesId == currentUserId
            || detail.AssignedStaffId == currentUserId;
        if (!canComplete)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden(
                "Only assigned staff, assigned Sales, or Admin can complete a schedule.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateCancelledTransition(
        string? role,
        ProjectScheduleStatus? currentStatus,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        if (currentStatus is not (ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED))
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest(
                "Only PENDING_CONFIRMATION or CONFIRMED schedules can be cancelled.");
        }

        var canCancel = role == AdminRole
            || role == SalesRole && detail.AssignedSalesId == currentUserId
            || role == CustomerRole && detail.CustomerId == currentUserId;
        if (!canCancel)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden(
                "You are not authorized to cancel this schedule.");
        }

        return null;
    }

    private static ProjectScheduleListQueryReadModel NormalizeQuery(ProjectScheduleListQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var limit = query.Limit;
        if (limit < 1)
        {
            limit = 20;
        }
        else if (limit > 100)
        {
            limit = 100;
        }

        return new ProjectScheduleListQueryReadModel
        {
            ScheduleType = query.ScheduleType,
            Status = query.Status,
            From = query.From,
            To = query.To,
            Page = page,
            Limit = limit
        };
    }

    private static Dictionary<string, string> BuildNotificationParameters(
        ProjectSchedule schedule,
        string projectName)
    {
        return new Dictionary<string, string>
        {
            ["ScheduleType"] = schedule.ScheduleType?.ToString() ?? string.Empty,
            ["ProjectName"] = projectName,
            ["ScheduledStart"] = schedule.ScheduledStart.ToString("f")
        };
    }

    private async Task DispatchScheduleCreatedAsync(
        ProjectSchedule schedule,
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        CancellationToken cancellationToken)
    {
        var receivers = BuildReceivers(project.CustomerId, schedule.AssignedStaffId);
        var parameters = BuildNotificationParameters(schedule, project.ProjectName);

        await _dispatcher.DispatchAsync(
            NotificationType.ProjectScheduleCreated,
            parameters,
            receivers,
            schedule.ProjectId,
            ProjectScheduleReferenceType,
            schedule.ScheduleId,
            cancellationToken);
    }

    private static List<Guid> BuildScheduleCreatedReceivers(
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        ProjectSchedule schedule)
    {
        var receivers = BuildReceivers(project.CustomerId, schedule.AssignedStaffId);
        if (schedule.ScheduleType == ProjectScheduleType.MEASUREMENT &&
            project.AssignedDesignerId.HasValue &&
            !receivers.Contains(project.AssignedDesignerId.Value))
        {
            receivers.Add(project.AssignedDesignerId.Value);
        }

        return receivers;
    }

    private async Task DispatchScheduleUpdatedAsync(
        ProjectSchedule schedule,
        ProjectScheduleDetailReadModel detail,
        CancellationToken cancellationToken)
    {
        var receivers = BuildReceivers(detail.CustomerId, schedule.AssignedStaffId);
        var parameters = BuildNotificationParameters(schedule, detail.ProjectName);

        await _dispatcher.DispatchAsync(
            NotificationType.ProjectScheduleUpdated,
            parameters,
            receivers,
            schedule.ProjectId,
            ProjectScheduleReferenceType,
            schedule.ScheduleId,
            cancellationToken);
    }

    private async Task DispatchStatusChangedAsync(
        ProjectSchedule schedule,
        ProjectScheduleDetailReadModel detail,
        ProjectScheduleStatus newStatus,
        CancellationToken cancellationToken)
    {
        var parameters = BuildNotificationParameters(schedule, detail.ProjectName);

        switch (newStatus)
        {
            case ProjectScheduleStatus.CONFIRMED:
            {
                var receivers = BuildReceivers(detail.AssignedSalesId, schedule.AssignedStaffId);
                await _dispatcher.DispatchAsync(
                    NotificationType.ProjectScheduleConfirmed,
                    parameters,
                    receivers,
                    schedule.ProjectId,
                    ProjectScheduleReferenceType,
                    schedule.ScheduleId,
                    cancellationToken);
                break;
            }
            case ProjectScheduleStatus.COMPLETED:
            {
                var receivers = BuildAllParticipants(detail, schedule);
                await _dispatcher.DispatchAsync(
                    NotificationType.ProjectScheduleCompleted,
                    parameters,
                    receivers,
                    schedule.ProjectId,
                    cancellationToken: cancellationToken);
                break;
            }
            case ProjectScheduleStatus.CANCELLED:
            {
                var receivers = BuildReceivers(detail.CustomerId, schedule.AssignedStaffId);
                await _dispatcher.DispatchAsync(
                    NotificationType.ProjectScheduleCancelled,
                    parameters,
                    receivers,
                    schedule.ProjectId,
                    ProjectScheduleReferenceType,
                    schedule.ScheduleId,
                    cancellationToken);
                break;
            }
        }
    }

    private static List<Guid> BuildReceivers(Guid? first, Guid? second)
    {
        var receivers = new List<Guid>();
        if (first.HasValue) receivers.Add(first.Value);
        if (second.HasValue && second.Value != first) receivers.Add(second.Value);
        return receivers;
    }

    private static List<Guid> BuildReceivers(Guid first, Guid? second)
    {
        return BuildReceivers((Guid?)first, second);
    }

    private static IReadOnlyList<Guid> BuildAllParticipants(
        ProjectScheduleDetailReadModel detail,
        ProjectSchedule schedule)
    {
        var receivers = new HashSet<Guid> { detail.CustomerId };
        if (detail.AssignedSalesId.HasValue) receivers.Add(detail.AssignedSalesId.Value);
        if (detail.AssignedDesignerId.HasValue) receivers.Add(detail.AssignedDesignerId.Value);
        if (schedule.AssignedStaffId.HasValue) receivers.Add(schedule.AssignedStaffId.Value);
        return [.. receivers];
    }

    private async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
