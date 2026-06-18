using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectService : IProjectService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const int MaxNoteLength = 1000;
    private const int MaxRejectionReasonLength = 1000;
    private const string ProjectReferenceType = "PROJECT";
    private const string AuthenticatedAccountIdRequiredMessage = "Authenticated account id is required.";
    private const string ProjectIdRequiredMessage = "Project id is required.";
    private const string ProjectNotFoundMessage = "Project not found.";
    private static readonly string[] ProjectSubmittedReceiverRoles = [SalesRole, AdminRole];
    private static readonly Dictionary<ProjectStatus, int> ProjectStatusRanks = new()
    {
        [ProjectStatus.SUBMITTED] = 10,
        [ProjectStatus.IN_CONSULTATION] = 20,
        [ProjectStatus.NEED_BASIC_INFORMATION] = 30,
        [ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT] = 40,
        [ProjectStatus.MEASUREMENT_REQUIRED] = 50,
        [ProjectStatus.SPACE_VERIFIED] = 60,
        [ProjectStatus.PROPOSAL_DRAFTING] = 70,
        [ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW] = 80,
        [ProjectStatus.REVISION_REQUESTED] = 90,
        [ProjectStatus.PROPOSAL_SELECTED] = 100,
        [ProjectStatus.QUOTATION_SENT] = 110,
        [ProjectStatus.QUOTATION_REVISION_REQUESTED] = 120,
        [ProjectStatus.ORDER_CONFIRMED] = 130,
        [ProjectStatus.IN_PRODUCTION] = 140,
        [ProjectStatus.PRODUCTION_BLOCKED] = 150,
        [ProjectStatus.READY_FOR_DELIVERY] = 160,
        [ProjectStatus.DELIVERING] = 170,
        [ProjectStatus.DELIVERED] = 180,
        [ProjectStatus.COMPLETED] = 190,
        [ProjectStatus.REJECTED] = 200
    };

    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<ProjectService>? _logger;

    public ProjectService(
        IProjectRepository projects,
        IUnitOfWork unitOfWork,
        INotificationDispatcher? notifications = null,
        ILogger<ProjectService>? logger = null)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<ProjectDto>> CreateAsync(
        Guid currentUserId,
        CreateProjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProjectDto>.BadRequest(errors);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName))
        {
            return ServiceResult<ProjectDto>.Forbidden("Only customer accounts can submit project requests.");
        }

        var now = DateTime.UtcNow;
        var year = now.Year;
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = currentUserId,
            ProjectCode = await GenerateProjectCodeAsync(year, cancellationToken),
            ProjectName = request.ProjectName.Trim(),
            BusinessType = request.BusinessType.Trim(),
            ProjectAddress = NormalizeOptional(request.ProjectAddress),
            BusinessPurpose = NormalizeOptional(request.BusinessPurpose),
            FurnitureRequirement = request.FurnitureRequirement.Trim(),
            Description = NormalizeOptional(request.Description),
            TotalAreaSqm = request.TotalAreaSqm,
            NumberOfFloors = request.NumberOfFloors,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            TargetCompletionDate = request.TargetCompletionDate,
            Status = ProjectStatus.SUBMITTED,
            SubmittedAt = now,
            AssignedSalesId = null,
            AssignedDesignerId = null
        };

        await _projects.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProjectSubmittedNotificationAsync(project, cancellationToken);

        return ServiceResult<ProjectDto>.Created(
            project.Adapt<ProjectDto>(),
            "Project request submitted successfully.");
    }

    private async Task DispatchProjectSubmittedNotificationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            var receiverIds = await _projects.GetActiveAccountIdsByRoleNamesAsync(
                ProjectSubmittedReceiverRoles,
                cancellationToken);
            if (receiverIds.Count == 0)
            {
                return;
            }

            var customerName = await _projects.GetAccountFullNameAsync(project.CustomerId, cancellationToken) ?? "Customer";
            await _notifications.DispatchAsync(
                NotificationType.ProjectRequestSubmitted,
                new Dictionary<string, string>
                {
                    ["CustomerName"] = customerName,
                    ["ProjectName"] = project.ProjectName
                },
                receiverIds,
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project request submitted notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    private async Task DispatchProjectAcceptedNotificationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectRequestAccepted,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName
                },
                [project.CustomerId],
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project request accepted notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    private async Task DispatchProjectMoreInformationRequestedNotificationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectMoreInformationRequested,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName
                },
                [project.CustomerId],
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project more information requested notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    private async Task DispatchProjectBasicInformationUpdatedNotificationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null || !project.AssignedSalesId.HasValue)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectBasicInformationUpdated,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName
                },
                [project.AssignedSalesId.Value],
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project basic information updated notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    private async Task DispatchProjectStatusChangedNotificationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var receiverIds = GetProjectParticipantIds(project);
        if (receiverIds.Count == 0)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectStatusChanged,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName,
                    ["Status"] = project.Status?.ToString() ?? string.Empty
                },
                receiverIds,
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project status changed notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    private async Task DispatchProjectDesignerAssignedNotificationAsync(
        Project project,
        Guid designerId,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectDesignerAssigned,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName
                },
                [designerId],
                project.ProjectId,
                ProjectReferenceType,
                project.ProjectId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project designer assigned notification for project {ProjectId}",
                project.ProjectId);
        }
    }

    public async Task<ServiceResult<ProjectListResponseDto>> GetListAsync(
        Guid currentUserId,
        ProjectListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectListResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationError = ValidatePagination(query.Page, query.Limit);
        if (validationError is not null)
        {
            return ServiceResult<ProjectListResponseDto>.BadRequest(validationError);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProjects(roleName))
        {
            return ServiceResult<ProjectListResponseDto>.Forbidden("You do not have access to view project requests.");
        }

        var repositoryQuery = query.Adapt<ProjectListQueryReadModel>();
        if (IsCustomer(roleName))
        {
            repositoryQuery.CustomerId = currentUserId;
        }

        var projects = await _projects.GetListAsync(repositoryQuery, cancellationToken);
        var total = await _projects.CountAsync(repositoryQuery, cancellationToken);
        var response = new ProjectListResponseDto
        {
            Items = projects.Adapt<List<ProjectListItemDto>>(),
            Page = query.Page,
            Limit = query.Limit,
            Total = total
        };

        return ServiceResult<ProjectListResponseDto>.Success(
            response,
            "Project request queue retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectDto>> GetByIdAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProjectDetail(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectDto>.Forbidden("You do not have access to view this project.");
        }

        return ServiceResult<ProjectDto>.Success(
            project.Adapt<ProjectDto>(),
            "Project detail retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectSalesAssignmentDto>> AssignSalesAsync(
        Guid projectId,
        Guid currentUserId,
        AssignProjectSalesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        if (NormalizeOptional(request.Note)?.Length > MaxNoteLength)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.BadRequest("Assignment note must not exceed 1000 characters.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAssignSales(roleName))
        {
            return ServiceResult<ProjectSalesAssignmentDto>.Forbidden("Only sales or admin accounts can accept project requests.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.NotFound(ProjectNotFoundMessage);
        }

        if (!IsPreConsultationStatus(project.Status))
        {
            return ServiceResult<ProjectSalesAssignmentDto>.BadRequest("Project cannot be accepted from its current status.");
        }

        if (project.AssignedSalesId.HasValue &&
            project.AssignedSalesId.Value != currentUserId &&
            !IsAdmin(roleName))
        {
            return ServiceResult<ProjectSalesAssignmentDto>.Conflict("Project is already assigned to another sales account.");
        }

        project.AssignedSalesId = currentUserId;
        project.Status = ProjectStatus.IN_CONSULTATION;
        project.SalesAssignedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProjectAcceptedNotificationAsync(project, cancellationToken);

        return ServiceResult<ProjectSalesAssignmentDto>.Success(
            project.Adapt<ProjectSalesAssignmentDto>(),
            "Project request accepted successfully.");
    }

    public async Task<ServiceResult<ProjectInformationRequestDto>> RequestInformationAsync(
        Guid projectId,
        Guid currentUserId,
        RequestProjectInformationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectInformationRequestDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectInformationRequestDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return ServiceResult<ProjectInformationRequestDto>.BadRequest("Request message is required.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAssignSales(roleName))
        {
            return ServiceResult<ProjectInformationRequestDto>.Forbidden("Only assigned sales or admin accounts can request more information.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectInformationRequestDto>.NotFound(ProjectNotFoundMessage);
        }

        if (!IsAdmin(roleName) && project.AssignedSalesId != currentUserId)
        {
            return ServiceResult<ProjectInformationRequestDto>.Forbidden("You do not have access to request more information for this project.");
        }

        var requestedAt = DateTime.UtcNow;
        project.Status = ProjectStatus.NEED_BASIC_INFORMATION;
        project.UpdatedAt = requestedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProjectMoreInformationRequestedNotificationAsync(project, cancellationToken);

        return ServiceResult<ProjectInformationRequestDto>.Success(
            project.Adapt<ProjectInformationRequestDto>(),
            "More information requested successfully.");
    }

    public async Task<ServiceResult<ProjectBasicInformationDto>> UpdateBasicInformationAsync(
        Guid projectId,
        Guid currentUserId,
        UpdateProjectBasicInformationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectBasicInformationDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectBasicInformationDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var errors = ValidateBasicInformation(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProjectBasicInformationDto>.BadRequest(errors);
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectBasicInformationDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanUpdateBasicInformation(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectBasicInformationDto>.Forbidden("You do not have access to update this project.");
        }

        if (!IsBasicInformationEditableStatus(project.Status))
        {
            return ServiceResult<ProjectBasicInformationDto>.BadRequest("Project basic information cannot be updated from its current status.");
        }

        var shouldNotifyAssignedSales = project.Status == ProjectStatus.NEED_BASIC_INFORMATION;
        ApplyBasicInformation(project, request);
        project.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (shouldNotifyAssignedSales)
        {
            await DispatchProjectBasicInformationUpdatedNotificationAsync(project, cancellationToken);
        }

        return ServiceResult<ProjectBasicInformationDto>.Success(
            project.Adapt<ProjectBasicInformationDto>(),
            "Project basic information updated successfully.");
    }

    public async Task<ServiceResult<ProjectStatusUpdateDto>> UpdateStatusAsync(
        Guid projectId,
        Guid currentUserId,
        UpdateProjectStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectStatusUpdateDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectStatusUpdateDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationError = ValidateDesignerAssignmentStatusRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<ProjectStatusUpdateDto>.BadRequest(validationError);
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectStatusUpdateDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanUpdateProjectStatus(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectStatusUpdateDto>.Forbidden("You do not have access to update this project status.");
        }

        if (project.Status != ProjectStatus.IN_CONSULTATION)
        {
            return ServiceResult<ProjectStatusUpdateDto>.BadRequest("Project must be in consultation before designer assignment.");
        }

        var missingInformation = GetMissingBasicInformation(project);
        if (missingInformation.Count > 0)
        {
            return ServiceResult<ProjectStatusUpdateDto>.BadRequest(
                missingInformation,
                "Project basic information is incomplete.");
        }

        project.Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT;
        project.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProjectStatusChangedNotificationAsync(project, cancellationToken);

        return ServiceResult<ProjectStatusUpdateDto>.Success(
            project.Adapt<ProjectStatusUpdateDto>(),
            "Project status updated successfully.");
    }

    public async Task<ServiceResult<ProjectRejectionDto>> RejectAsync(
        Guid projectId,
        Guid currentUserId,
        RejectProjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectRejectionDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectRejectionDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var rejectionReason = NormalizeOptional(request.RejectionReason);
        var reasonValidationError = ValidateRejectionReason(rejectionReason);
        if (reasonValidationError is not null)
        {
            return ServiceResult<ProjectRejectionDto>.BadRequest(reasonValidationError);
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectRejectionDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanRejectProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectRejectionDto>.Forbidden("You do not have access to reject this project.");
        }

        if (!CanRejectFromStatus(project.Status))
        {
            return ServiceResult<ProjectRejectionDto>.BadRequest("Project cannot be rejected from its current status.");
        }

        project.Status = ProjectStatus.REJECTED;
        project.RejectionReason = rejectionReason;
        project.RejectedAt = DateTime.UtcNow;
        project.UpdatedAt = project.RejectedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectRejectionDto>.Success(
            project.Adapt<ProjectRejectionDto>(),
            "Project request rejected.");
    }

    public async Task<ServiceResult<ProjectDesignerAssignmentDto>> AssignDesignerAsync(
        Guid projectId,
        Guid currentUserId,
        AssignProjectDesignerRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.BadRequest(ProjectIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var validationError = ValidateDesignerAssignmentRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.BadRequest(validationError);
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAssignDesigner(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.Forbidden("You do not have access to assign a designer to this project.");
        }

        if (!project.AssignedSalesId.HasValue)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.BadRequest("Project must have assigned sales before designer assignment.");
        }

        if (project.Status != ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.BadRequest("Project must be waiting for designer assignment.");
        }

        var designer = await _projects.GetActiveDesignerAsync(request.DesignerId, cancellationToken);
        if (designer is null)
        {
            return ServiceResult<ProjectDesignerAssignmentDto>.BadRequest("Designer account is not active or does not have Designer role.");
        }

        project.AssignedDesignerId = designer.AccountId;
        project.DesignerAssignedAt = DateTime.UtcNow;
        project.Status = ResolveDesignerAssignmentStatus(request.SpaceDataStatus!.Value);
        project.UpdatedAt = project.DesignerAssignedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchProjectDesignerAssignedNotificationAsync(project, designer.AccountId, cancellationToken);

        return ServiceResult<ProjectDesignerAssignmentDto>.Success(
            new ProjectDesignerAssignmentDto
            {
                ProjectId = project.ProjectId,
                AssignedDesigner = designer.Adapt<AssignedDesignerDto>(),
                Status = project.Status,
                DesignerAssignedAt = project.DesignerAssignedAt
            },
            "Designer assigned successfully.");
    }

    private async Task<string> GenerateProjectCodeAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var sequence = await _projects.CountSubmittedInYearAsync(year, cancellationToken) + 1;
        return $"PRJ-{year}-{sequence:0000}";
    }

    private static List<string> ValidateRequest(CreateProjectRequestDto request)
    {
        return ValidateBasicInformation(request.Adapt<UpdateProjectBasicInformationRequestDto>());
    }

    private static List<string> ValidateBasicInformation(UpdateProjectBasicInformationRequestDto request)
    {
        var errors = new List<string>();
        AddRequiredStringError(errors, request.ProjectName, "Project name is required.");
        AddRequiredStringError(errors, request.BusinessType, "Business type is required.");
        AddRequiredStringError(errors, request.FurnitureRequirement, "Furniture requirement is required.");

        if (!string.IsNullOrWhiteSpace(request.ProjectName) &&
            request.ProjectName.Trim().Length > 150)
        {
            errors.Add("Project name must not exceed 150 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType) &&
            request.BusinessType.Trim().Length > 100)
        {
            errors.Add("Business type must not exceed 100 characters.");
        }

        if (request.TotalAreaSqm is < 0)
        {
            errors.Add("Total area must be greater than or equal to zero.");
        }

        if (request.NumberOfFloors is < 0)
        {
            errors.Add("Number of floors must be greater than or equal to zero.");
        }

        if (request.BudgetMin is < 0)
        {
            errors.Add("Minimum budget must be greater than or equal to zero.");
        }

        if (request.BudgetMax is < 0)
        {
            errors.Add("Maximum budget must be greater than or equal to zero.");
        }

        if (request.BudgetMin.HasValue &&
            request.BudgetMax.HasValue &&
            request.BudgetMin.Value > request.BudgetMax.Value)
        {
            errors.Add("Minimum budget must be less than or equal to maximum budget.");
        }

        if (request.TargetCompletionDate.HasValue &&
            request.TargetCompletionDate.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            errors.Add("Target completion date must not be in the past.");
        }

        return errors;
    }

    private static void ApplyBasicInformation(Project project, UpdateProjectBasicInformationRequestDto request)
    {
        project.ProjectName = request.ProjectName.Trim();
        project.BusinessType = request.BusinessType.Trim();
        project.ProjectAddress = NormalizeOptional(request.ProjectAddress);
        project.BusinessPurpose = NormalizeOptional(request.BusinessPurpose);
        project.FurnitureRequirement = request.FurnitureRequirement.Trim();
        project.Description = NormalizeOptional(request.Description);
        project.TotalAreaSqm = request.TotalAreaSqm;
        project.NumberOfFloors = request.NumberOfFloors;
        project.BudgetMin = request.BudgetMin;
        project.BudgetMax = request.BudgetMax;
        project.TargetCompletionDate = request.TargetCompletionDate;
    }

    private static void AddRequiredStringError(List<string> errors, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(message);
        }
    }

    private static string? ValidatePagination(int page, int limit)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (limit is < 1 or > 100)
        {
            return "Limit must be between 1 and 100.";
        }

        return null;
    }

    private static bool CanViewProjects(string? roleName)
    {
        return IsCustomer(roleName) ||
            IsAdmin(roleName) ||
            string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanViewProjectDetail(
        ProjectDetailReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return project.CustomerId == currentUserId;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            // Sales can inspect unassigned projects; once assigned, only assigned sales can view.
            if (!project.AssignedSalesId.HasValue && !project.AssignedDesignerId.HasValue)
            {
                return true;
            }

            return project.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return false;
    }

    private static string? ValidateDesignerAssignmentStatusRequest(UpdateProjectStatusRequestDto request)
    {
        if (request.Status is null)
        {
            return "Project status is required.";
        }

        if (request.Status != ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT)
        {
            return "Only WAITING_FOR_DESIGNER_ASSIGNMENT transition is supported.";
        }

        if (NormalizeOptional(request.Note)?.Length > MaxNoteLength)
        {
            return "Status update note must not exceed 1000 characters.";
        }

        return null;
    }

    private static string? ValidateRejectionReason(string? rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return "Rejection reason is required.";
        }

        if (rejectionReason.Length > MaxRejectionReasonLength)
        {
            return "Rejection reason must not exceed 1000 characters.";
        }

        return null;
    }

    private static string? ValidateDesignerAssignmentRequest(AssignProjectDesignerRequestDto request)
    {
        if (request.DesignerId == Guid.Empty)
        {
            return "Designer id is required.";
        }

        if (request.SpaceDataStatus is null)
        {
            return "Space data status is required.";
        }

        if (NormalizeOptional(request.Note)?.Length > MaxNoteLength)
        {
            return "Designer assignment note must not exceed 1000 characters.";
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

    private static IReadOnlyList<Guid> GetProjectParticipantIds(Project project)
    {
        var receiverIds = new List<Guid> { project.CustomerId };

        if (project.AssignedSalesId.HasValue)
        {
            receiverIds.Add(project.AssignedSalesId.Value);
        }

        if (project.AssignedDesignerId.HasValue)
        {
            receiverIds.Add(project.AssignedDesignerId.Value);
        }

        return receiverIds.Distinct().ToList();
    }

    private static bool CanUpdateBasicInformation(Project project, Guid currentUserId, string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return project.CustomerId == currentUserId;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            return project.AssignedSalesId == currentUserId;
        }

        return false;
    }

    private static bool CanUpdateProjectStatus(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase) &&
                project.AssignedSalesId == currentUserId);
    }

    private static bool CanRejectProject(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase) &&
                project.AssignedSalesId == currentUserId);
    }

    private static bool CanAssignDesigner(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase) &&
                project.AssignedSalesId == currentUserId);
    }

    private static bool CanAssignSales(string? roleName)
    {
        return IsAdmin(roleName) ||
            string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreConsultationStatus(ProjectStatus? status)
    {
        return status is ProjectStatus.SUBMITTED or ProjectStatus.NEED_BASIC_INFORMATION;
    }

    private static bool IsBasicInformationEditableStatus(ProjectStatus? status)
    {
        return status is ProjectStatus.SUBMITTED
            or ProjectStatus.NEED_BASIC_INFORMATION
            or ProjectStatus.IN_CONSULTATION;
    }

    private static bool CanRejectFromStatus(ProjectStatus? status)
    {
        if (!status.HasValue ||
            !ProjectStatusRanks.TryGetValue(status.Value, out var currentRank))
        {
            return false;
        }

        return currentRank < ProjectStatusRanks[ProjectStatus.ORDER_CONFIRMED];
    }

    private static ProjectStatus ResolveDesignerAssignmentStatus(ProjectSpaceDataStatus spaceDataStatus)
    {
        return spaceDataStatus == ProjectSpaceDataStatus.SUFFICIENT
            ? ProjectStatus.SPACE_VERIFIED
            : ProjectStatus.MEASUREMENT_REQUIRED;
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
