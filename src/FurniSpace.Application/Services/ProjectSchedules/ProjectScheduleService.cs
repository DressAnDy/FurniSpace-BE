using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.ProjectSchedules.ProjectScheduleServiceConstants;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.ProjectSchedules;

public sealed class ProjectScheduleService : IProjectScheduleService
{
    private readonly IProjectScheduleRepository _schedules;
    private readonly IProjectRepository _projects;
    private readonly IProjectFileRepository _files;
    private readonly IOrderRepository _orders;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectWorkflowSettings _workflowSettings;

    public ProjectScheduleService(
        IProjectScheduleRepository schedules,
        IProjectRepository projects,
        IProjectFileRepository files,
        IOrderRepository orders,
        IProductionRequestRepository productionRequests,
        ProjectScheduleServiceDependencies dependencies)
    {
        _schedules = schedules;
        _projects = projects;
        _files = files;
        _orders = orders;
        _productionRequests = productionRequests;
        _dispatcher = dependencies.Dispatcher;
        _unitOfWork = dependencies.UnitOfWork;
        _workflowSettings = dependencies.WorkflowSettings;
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
        if (!CanCreateSchedules(role))
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("Only assigned Sales or Admin can create schedules.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound("Project not found.");
        }

        if (role == ApplicationRoles.Sales && project.AssignedSalesId != currentUserId)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("You are not the assigned Sales for this project.");
        }

        var businessRuleError = await ValidateCreateScheduleBusinessRulesAsync(
            role,
            project,
            projectId,
            currentUserId,
            request,
            cancellationToken);
        if (businessRuleError is not null)
        {
            return businessRuleError;
        }

        var now = DateTime.UtcNow;
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

        if (!await CanViewProjectSchedulesAsync(role, project, currentUserId, cancellationToken))
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
        if (!await CanAccessScheduleAsync(role, detail, currentUserId, cancellationToken))
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
        var authorizationError = ValidateUpdatePermission(role, detail, currentUserId, request);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        var validationError = await ValidateUpdateTimeAsync(request, detail, cancellationToken);
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

        if (newStatus == ProjectScheduleStatus.COMPLETED &&
            detail.ScheduleType == ProjectScheduleType.MEASUREMENT &&
            _workflowSettings.RequireMeasurementFileOnScheduleComplete)
        {
            var fileError = await ProjectMeasurementGate.ValidateMeasurementFilesAsync(
                detail.ProjectId,
                _files,
                cancellationToken);
            if (fileError is not null)
            {
                return ServiceResult<ProjectScheduleDto>.Failure(fileError);
            }
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

        var response = schedule.Adapt<ProjectScheduleDto>();
        if (newStatus == ProjectScheduleStatus.COMPLETED &&
            detail.ScheduleType == ProjectScheduleType.MEASUREMENT)
        {
            var project = await _projects.GetByIdAsync(detail.ProjectId, cancellationToken);
            if (project is not null)
            {
                var hasCompletedMeasurement = await _schedules.HasCompletedMeasurementScheduleAsync(
                    detail.ProjectId,
                    cancellationToken);
                response.CanMoveToProposalConsulting = ProjectMeasurementGate.CanMoveToProposalConsulting(
                    project,
                    hasCompletedMeasurement);
            }
        }

        return ServiceResult<ProjectScheduleDto>.Success(
            response,
            $"Schedule status updated to {newStatus}.");
    }

    public async Task<ServiceResult<ProjectScheduleDto>> DeleteAsync(
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
        var permissionError = ValidateDeletePermission(role, detail, currentUserId);
        if (permissionError is not null)
        {
            return permissionError;
        }

        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        await ExecuteInTransactionAsync(
            ct =>
            {
                _schedules.Remove(schedule);
                return _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<ProjectScheduleDto>.Success(
            schedule.Adapt<ProjectScheduleDto>(),
            "Project schedule deleted successfully.");
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

        Guid? staffId = role == ApplicationRoles.Admin ? null : currentUserId;

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

    private async Task<bool> CanViewProjectSchedulesAsync(
        string? role,
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return role switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Customer => project.CustomerId == currentUserId,
            ApplicationRoles.Sales => project.AssignedSalesId == currentUserId,
            ApplicationRoles.Designer => project.AssignedDesignerId == currentUserId,
            ApplicationRoles.Production => await CanProductionViewProjectScheduleContextAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken),
            _ => false
        };
    }

    private async Task<bool> CanAccessScheduleAsync(
        string? role,
        ProjectScheduleDetailReadModel schedule,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (role == ApplicationRoles.Admin) return true;
        if (schedule.CustomerId == currentUserId) return true;
        if (schedule.AssignedSalesId == currentUserId) return true;
        if (schedule.AssignedDesignerId == currentUserId) return true;
        if (schedule.AssignedStaffId == currentUserId) return true;

        if (IsProduction(role))
        {
            return await CanProductionViewProjectScheduleContextAsync(
                schedule.ProjectId,
                currentUserId,
                cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanProductionViewProjectScheduleContextAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _schedules.HasAssignedScheduleAsync(projectId, currentUserId, cancellationToken) ||
            await _productionRequests.HasViewableAssignedRequestAsync(projectId, currentUserId, cancellationToken);
    }

    private async Task<ServiceResult<ProjectScheduleDto>?> ValidateCreateScheduleBusinessRulesAsync(
        string? role,
        ProjectDetailReadModel project,
        Guid projectId,
        Guid currentUserId,
        CreateProjectScheduleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (IsProduction(role))
        {
            var productionAccessError = await ValidateProductionCreatePermissionAsync(
                projectId,
                currentUserId,
                request,
                cancellationToken);
            if (productionAccessError is not null)
            {
                return productionAccessError;
            }
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

        var targetDateError = ValidateScheduleDatesAgainstTarget(
            request.ScheduledStart,
            request.ScheduledEnd,
            project.TargetCompletionDate);
        if (targetDateError is not null)
        {
            return targetDateError;
        }

        if (request.ScheduleType == ProjectScheduleType.MEASUREMENT)
        {
            var measurementError = ValidateMeasurementScheduleCreate(
                role,
                project,
                request.AssignedStaffId);
            if (measurementError is not null)
            {
                return measurementError;
            }
        }

        if (request.ScheduleType == ProjectScheduleType.DELIVERY)
        {
            return await ValidateDeliveryScheduleCreateAsync(project, cancellationToken);
        }

        return null;
    }

    private async Task<ServiceResult<ProjectScheduleDto>?> ValidateProductionCreatePermissionAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectScheduleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsProductionManageableType(request.ScheduleType))
        {
            return InvalidScheduleTypeResult("Production staff can only create DELIVERY, HANDOVER, or OTHER schedules.");
        }

        if (request.AssignedStaffId == currentUserId)
        {
            return null;
        }

        var hasRelatedSchedule = await _schedules.HasAssignedScheduleAsync(
            projectId,
            currentUserId,
            cancellationToken);
        return hasRelatedSchedule
            ? null
            : ServiceResult<ProjectScheduleDto>.Forbidden(
                "Production staff can only create schedules for assigned production work.");
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

        if (role != ApplicationRoles.Admin && assignedStaffId.Value != project.AssignedDesignerId.Value)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden(
                "Measurement schedules must be assigned to the project's designer.");
        }

        return null;
    }

    private async Task<ServiceResult<ProjectScheduleDto>?> ValidateDeliveryScheduleCreateAsync(
        FurniSpace.Infrastructure.ReadModels.Projects.ProjectDetailReadModel project,
        CancellationToken cancellationToken)
    {
        if (!IsDeliveryReadyProject(project.Status))
        {
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.BadRequest(
                    ProjectScheduleErrorCodes.OrderNotReadyForDelivery,
                    "Project and order must be ready for delivery before creating a delivery schedule."));
        }

        if (await _orders.HasCompletedDeliveryFlowAsync(project.ProjectId, cancellationToken))
        {
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Conflict(
                    ProjectScheduleErrorCodes.DeliveryScheduleNotAllowedAfterCompletion,
                    "A new delivery schedule cannot be created after delivery has completed."));
        }

        if (await _schedules.HasActiveDeliveryScheduleAsync(project.ProjectId, cancellationToken))
        {
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Conflict(
                    ProjectScheduleErrorCodes.ActiveDeliveryScheduleExists,
                    "Only one active delivery schedule is allowed per project."));
        }

        var hasReadyOrder = await _orders.HasProjectOrderInStatusesAsync(
            project.ProjectId,
            DeliveryReadyOrderStatuses,
            cancellationToken);
        return hasReadyOrder
            ? null
            : ServiceResult<ProjectScheduleDto>.Failure(
                Error.BadRequest(
                    ProjectScheduleErrorCodes.OrderNotReadyForDelivery,
                    "Project and order must be ready for delivery before creating a delivery schedule."));
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateUpdatePermission(
        string? role,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId,
        UpdateProjectScheduleRequestDto request)
    {
        if (IsProduction(role))
        {
            return ValidateProductionUpdatePermission(detail, currentUserId, request);
        }

        if (role != ApplicationRoles.Admin &&
            !(role == ApplicationRoles.Sales && detail.AssignedSalesId == currentUserId))
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("Only assigned Sales or Admin can update schedules.");
        }

        if (role != ApplicationRoles.Admin &&
            detail.Status is not (ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED))
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Completed or cancelled schedules cannot be updated.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateProductionUpdatePermission(
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId,
        UpdateProjectScheduleRequestDto request)
    {
        if (!IsProductionManageableType(detail.ScheduleType))
        {
            return InvalidScheduleTypeResult("Production staff can only update DELIVERY, HANDOVER, or OTHER schedules.");
        }

        if (detail.AssignedStaffId != currentUserId)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("You are not assigned to this production schedule.");
        }

        if (request.AssignedStaffId.HasValue && request.AssignedStaffId.Value != currentUserId)
        {
            return ServiceResult<ProjectScheduleDto>.Forbidden("Production staff cannot reassign schedules.");
        }

        if (detail.Status is ProjectScheduleStatus.COMPLETED or ProjectScheduleStatus.CANCELLED)
        {
            return ServiceResult<ProjectScheduleDto>.BadRequest("Completed or cancelled schedules cannot be updated.");
        }

        return null;
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateDeletePermission(
        string? role,
        ProjectScheduleDetailReadModel detail,
        Guid currentUserId)
    {
        if (role == ApplicationRoles.Admin ||
            role == ApplicationRoles.Sales && detail.AssignedSalesId == currentUserId)
        {
            return null;
        }

        if (IsProduction(role))
        {
            if (!IsProductionManageableType(detail.ScheduleType))
            {
                return InvalidScheduleTypeResult("Production staff can only delete DELIVERY, HANDOVER, or OTHER schedules.");
            }

            return detail.AssignedStaffId == currentUserId
                ? null
                : ServiceResult<ProjectScheduleDto>.Forbidden("You are not assigned to this production schedule.");
        }

        return ServiceResult<ProjectScheduleDto>.Forbidden("You are not authorized to delete this schedule.");
    }

    private async Task<ServiceResult<ProjectScheduleDto>?> ValidateUpdateTimeAsync(
        UpdateProjectScheduleRequestDto request,
        ProjectScheduleDetailReadModel detail,
        CancellationToken cancellationToken)
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

        var project = await _projects.GetDetailAsync(detail.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectScheduleDto>.NotFound(ScheduleNotFoundMessage);
        }

        return ValidateScheduleDatesAgainstTarget(
            effectiveStart,
            effectiveEnd,
            project.TargetCompletionDate);
    }

    private static ServiceResult<ProjectScheduleDto>? ValidateScheduleDatesAgainstTarget(
        DateTime scheduledStart,
        DateTime? scheduledEnd,
        DateOnly? targetCompletionDate)
    {
        var startError = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(
            scheduledStart,
            targetCompletionDate);
        if (startError is not null)
        {
            return ServiceResult<ProjectScheduleDto>.Failure(startError);
        }

        if (scheduledEnd.HasValue)
        {
            var endError = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(
                scheduledEnd.Value,
                targetCompletionDate);
            if (endError is not null)
            {
                return ServiceResult<ProjectScheduleDto>.Failure(endError);
            }
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
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Validation(
                    ProjectScheduleErrorCodes.InvalidScheduleStatus,
                    $"Cannot transition from terminal status {currentStatus}."));
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
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Validation(
                    ProjectScheduleErrorCodes.InvalidScheduleStatus,
                    "Only PENDING_CONFIRMATION schedules can be confirmed."));
        }

        if (role != ApplicationRoles.Customer || detail.CustomerId != currentUserId)
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
            return ServiceResult<ProjectScheduleDto>.Failure(
                Error.Validation(
                ProjectScheduleErrorCodes.InvalidScheduleStatus,
                    "Only CONFIRMED schedules can be completed."));
        }

        if (IsProduction(role))
        {
            if (!IsProductionStatusType(detail.ScheduleType))
            {
                return InvalidScheduleTypeResult("Production staff can only complete DELIVERY or HANDOVER schedules.");
            }

            return detail.AssignedStaffId == currentUserId
                ? null
                : ServiceResult<ProjectScheduleDto>.Forbidden(
                    "Only assigned production staff can complete this schedule.");
        }

        var canComplete = role == ApplicationRoles.Admin
            || role == ApplicationRoles.Sales && detail.AssignedSalesId == currentUserId
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

        var canCancel = role == ApplicationRoles.Admin
            || role == ApplicationRoles.Sales && detail.AssignedSalesId == currentUserId
            || role == ApplicationRoles.Customer && detail.CustomerId == currentUserId;
        if (IsProduction(role))
        {
            if (!IsProductionStatusType(detail.ScheduleType))
            {
                return InvalidScheduleTypeResult("Production staff can only cancel DELIVERY or HANDOVER schedules.");
            }

            canCancel = detail.AssignedStaffId == currentUserId;
        }

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
        var receivers = BuildScheduleCreatedReceivers(project, schedule);
        var parameters = BuildNotificationParameters(schedule, project.ProjectName);

        await _dispatcher.DispatchAsync(
            NotificationType.ProjectScheduleCreated,
            parameters,
            receivers,
            new NotificationDispatchRequest(
                schedule.ProjectId,
                ProjectScheduleReferenceType,
                schedule.ScheduleId),
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
            new NotificationDispatchRequest(
                schedule.ProjectId,
                ProjectScheduleReferenceType,
                schedule.ScheduleId),
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
                    new NotificationDispatchRequest(
                        schedule.ProjectId,
                        ProjectScheduleReferenceType,
                        schedule.ScheduleId),
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
                    new NotificationDispatchRequest(schedule.ProjectId),
                    cancellationToken);
                break;
            }
            case ProjectScheduleStatus.CANCELLED:
            {
                var receivers = BuildReceivers(detail.CustomerId, schedule.AssignedStaffId);
                await _dispatcher.DispatchAsync(
                    NotificationType.ProjectScheduleCancelled,
                    parameters,
                    receivers,
                    new NotificationDispatchRequest(
                        schedule.ProjectId,
                        ProjectScheduleReferenceType,
                        schedule.ScheduleId),
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

    private static bool CanCreateSchedules(string? role)
    {
        return role is ApplicationRoles.Admin or ApplicationRoles.Sales or ApplicationRoles.Production;
    }

    private static bool IsProduction(string? role)
    {
        return string.Equals(role, ApplicationRoles.Production, StringComparison.Ordinal);
    }

    private static bool IsProductionManageableType(ProjectScheduleType? scheduleType)
    {
        return scheduleType.HasValue && ProductionManageableScheduleTypes.Contains(scheduleType.Value);
    }

    private static bool IsProductionStatusType(ProjectScheduleType? scheduleType)
    {
        return scheduleType.HasValue && ProductionStatusScheduleTypes.Contains(scheduleType.Value);
    }

    private static bool IsDeliveryReadyProject(ProjectStatus? status)
    {
        return status is ProjectStatus.READY_FOR_DELIVERY or ProjectStatus.DELIVERING;
    }

    private static ServiceResult<ProjectScheduleDto> InvalidScheduleTypeResult(string message)
    {
        return ServiceResult<ProjectScheduleDto>.Failure(
            Error.Validation(ProjectScheduleErrorCodes.InvalidScheduleType, message));
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

public sealed record ProjectScheduleServiceDependencies(
    IUnitOfWork UnitOfWork,
    INotificationDispatcher Dispatcher,
    ProjectWorkflowSettings WorkflowSettings);
