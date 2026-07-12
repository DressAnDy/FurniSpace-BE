using FurniSpace.Application.Common;
using static FurniSpace.Application.Constants.ProjectAreas.ProjectAreaServiceConstants;
using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Application.Interfaces.ProjectAreas;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.ProjectAreas;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.ProjectAreas;

public sealed class ProjectAreaService : IProjectAreaService
{
    private readonly IProjectAreaRepository _areas;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectAreaService(
        IProjectAreaRepository areas,
        IProjectRepository projects,
        IUnitOfWork unitOfWork)
    {
        _areas = areas;
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectAreaDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Project id is required.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectAreaDto>.NotFound(ProjectNotFoundMessage);
        }

        if (!CanManageProjectAreas(project, currentUserId, role))
        {
            return ServiceResult<ProjectAreaDto>.Forbidden("You do not have access to create project areas.");
        }

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var parentError = await ValidateParentAreaAsync(
            projectId,
            request.ParentAreaId,
            projectAreaId: null,
            cancellationToken);
        if (parentError is not null)
        {
            return parentError;
        }

        var now = DateTime.UtcNow;
        var area = request.Adapt<ProjectArea>();
        area.ProjectAreaId = Guid.NewGuid();
        area.ProjectId = projectId;
        area.CreatedBy = currentUserId;
        area.CreatedAt = now;
        area.UpdatedAt = now;

        await _areas.AddAsync(area, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectAreaDto>.Created(
            area.Adapt<ProjectAreaDto>(),
            "Project area created successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<ProjectAreaDto>>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        bool includeCancelled,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<IReadOnlyList<ProjectAreaDto>>.Unauthorized();
        }

        if (projectId == Guid.Empty)
        {
            return ServiceResult<IReadOnlyList<ProjectAreaDto>>.BadRequest("Project id is required.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<IReadOnlyList<ProjectAreaDto>>.NotFound(ProjectNotFoundMessage);
        }

        if (!CanViewProjectAreas(project, currentUserId, role))
        {
            return ServiceResult<IReadOnlyList<ProjectAreaDto>>.Forbidden("You do not have access to this project's areas.");
        }

        var items = await _areas.GetListByProjectAsync(projectId, includeCancelled, cancellationToken);
        return ServiceResult<IReadOnlyList<ProjectAreaDto>>.Success(
            items.Adapt<IReadOnlyList<ProjectAreaDto>>(),
            "Project areas retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectAreaDto>> GetDetailAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAreaReadContextAsync(projectAreaId, currentUserId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return ServiceResult<ProjectAreaDto>.Success(
            context.Detail!.Adapt<ProjectAreaDto>(),
            "Project area retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectAreaDto>> UpdateAsync(
        Guid projectAreaId,
        Guid currentUserId,
        UpdateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAreaManageContextAsync(
            projectAreaId,
            currentUserId,
            "You do not have access to update this project area.",
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var detail = context.Detail!;

        var dimensionError = ValidateDimensions(
            request.AreaSqm,
            request.Width,
            request.Length,
            request.Height);
        if (dimensionError is not null)
        {
            return dimensionError;
        }

        if (request.ParentAreaId.HasValue)
        {
            var parentError = await ValidateParentAreaAsync(
                detail.ProjectId,
                request.ParentAreaId,
                projectAreaId,
                cancellationToken);
            if (parentError is not null)
            {
                return parentError;
            }
        }

        var area = await _areas.GetByIdAsync(projectAreaId, cancellationToken);
        if (area is null)
        {
            return ProjectAreaNotFoundResult();
        }

        request.Adapt(area);
        area.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        return ServiceResult<ProjectAreaDto>.Success(
            updated!.Adapt<ProjectAreaDto>(),
            "Project area updated successfully.");
    }

    public async Task<ServiceResult<ProjectAreaDto>> CancelAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAreaManageContextAsync(
            projectAreaId,
            currentUserId,
            "You do not have access to cancel this project area.",
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var detail = context.Detail!;

        if (detail.Status == ProjectAreaStatus.CANCELLED)
        {
            return ServiceResult<ProjectAreaDto>.Success(
                detail.Adapt<ProjectAreaDto>(),
                "Project area is already cancelled.");
        }

        if (await _areas.HasActiveUsageAsync(projectAreaId, cancellationToken))
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.Validation(
                    ProjectAreaErrorCodes.ProjectAreaInUse,
                    "Project area is in use by proposal scenes or proposal items."));
        }

        var area = await _areas.GetByIdAsync(projectAreaId, cancellationToken);
        if (area is null)
        {
            return ProjectAreaNotFoundResult();
        }

        area.Status = ProjectAreaStatus.CANCELLED;
        area.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        detail.Status = ProjectAreaStatus.CANCELLED;
        detail.UpdatedAt = area.UpdatedAt;

        return ServiceResult<ProjectAreaDto>.Success(
            detail.Adapt<ProjectAreaDto>(),
            "Project area cancelled successfully.");
    }

    private async Task<(ProjectAreaDetailReadModel? Detail, ServiceResult<ProjectAreaDto>? Error)> ResolveAreaReadContextAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var authError = ValidateProjectAreaAccessRequest(projectAreaId, currentUserId);
        if (authError is not null)
        {
            return (null, authError);
        }

        var detail = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        if (detail is null)
        {
            return (null, ProjectAreaNotFoundResult());
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProjectArea(detail, currentUserId, role))
        {
            return (null, ServiceResult<ProjectAreaDto>.Forbidden("You do not have access to this project area."));
        }

        return (detail, null);
    }

    private async Task<(ProjectAreaDetailReadModel? Detail, ServiceResult<ProjectAreaDto>? Error)> ResolveAreaManageContextAsync(
        Guid projectAreaId,
        Guid currentUserId,
        string forbiddenMessage,
        CancellationToken cancellationToken)
    {
        var authError = ValidateProjectAreaAccessRequest(projectAreaId, currentUserId);
        if (authError is not null)
        {
            return (null, authError);
        }

        var detail = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        if (detail is null)
        {
            return (null, ProjectAreaNotFoundResult());
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageProjectArea(detail, currentUserId, role))
        {
            return (null, ServiceResult<ProjectAreaDto>.Forbidden(forbiddenMessage));
        }

        return (detail, null);
    }

    private static ServiceResult<ProjectAreaDto>? ValidateProjectAreaAccessRequest(
        Guid projectAreaId,
        Guid currentUserId)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.Unauthorized();
        }

        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest(ProjectAreaIdRequiredMessage);
        }

        return null;
    }

    private static ServiceResult<ProjectAreaDto> ProjectAreaNotFoundResult()
    {
        return ServiceResult<ProjectAreaDto>.Failure(
            Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
    }

    private static ServiceResult<ProjectAreaDto>? ValidateCreateRequest(CreateProjectAreaRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.AreaName))
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Area name is required.");
        }

        if (!request.AreaType.HasValue)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Area type is required.");
        }

        return ValidateDimensions(request.AreaSqm, request.Width, request.Length, request.Height);
    }

    private static ServiceResult<ProjectAreaDto>? ValidateDimensions(
        decimal? areaSqm,
        decimal? width,
        decimal? length,
        decimal? height)
    {
        if (IsNegative(areaSqm) || IsNegative(width) || IsNegative(length) || IsNegative(height))
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.Validation(
                    ProjectAreaErrorCodes.InvalidAreaDimension,
                    "Area dimensions must not be negative."));
        }

        return null;
    }

    private async Task<ServiceResult<ProjectAreaDto>?> ValidateParentAreaAsync(
        Guid projectId,
        Guid? parentAreaId,
        Guid? projectAreaId,
        CancellationToken cancellationToken)
    {
        if (!parentAreaId.HasValue)
        {
            return null;
        }

        if (projectAreaId.HasValue && parentAreaId.Value == projectAreaId.Value)
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.Validation(
                    ProjectAreaErrorCodes.InvalidParentArea,
                    "Project area cannot be its own parent."));
        }

        var belongsToProject = await _areas.BelongsToProjectAsync(
            parentAreaId.Value,
            projectId,
            cancellationToken);
        if (!belongsToProject)
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.Validation(
                    ProjectAreaErrorCodes.InvalidParentArea,
                    "Parent area must belong to the same project."));
        }

        return null;
    }

    private static bool CanManageProjectAreas(
        ProjectDetailReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        return CanManageStaffAssignment(
            ToStaffAssignment(project),
            currentUserId,
            roleName);
    }

    private static bool CanManageProjectArea(
        ProjectAreaDetailReadModel area,
        Guid currentUserId,
        string? roleName)
    {
        return CanManageStaffAssignment(
            ToStaffAssignment(area),
            currentUserId,
            roleName);
    }

    private static bool CanViewProjectAreas(
        ProjectDetailReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        return CanViewParticipantAccess(
            ToParticipantAccess(project),
            currentUserId,
            roleName);
    }

    private static bool CanViewProjectArea(
        ProjectAreaDetailReadModel area,
        Guid currentUserId,
        string? roleName)
    {
        return CanViewParticipantAccess(
            ToParticipantAccess(area),
            currentUserId,
            roleName);
    }

    private static bool CanManageStaffAssignment(
        ProjectStaffAssignment assignment,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            return assignment.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase))
        {
            return assignment.AssignedDesignerId == currentUserId;
        }

        return false;
    }

    private static bool CanViewParticipantAccess(
        ProjectParticipantAccess access,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return access.CustomerId == currentUserId;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            if (!access.AssignedSalesId.HasValue && !access.AssignedDesignerId.HasValue)
            {
                return true;
            }

            return access.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase))
        {
            return access.AssignedDesignerId == currentUserId;
        }

        return false;
    }

    private static ProjectStaffAssignment ToStaffAssignment(ProjectDetailReadModel project)
    {
        return new ProjectStaffAssignment(project.AssignedSalesId, project.AssignedDesignerId);
    }

    private static ProjectStaffAssignment ToStaffAssignment(ProjectAreaDetailReadModel area)
    {
        return new ProjectStaffAssignment(area.AssignedSalesId, area.AssignedDesignerId);
    }

    private static ProjectParticipantAccess ToParticipantAccess(ProjectDetailReadModel project)
    {
        return new ProjectParticipantAccess(
            project.CustomerId,
            project.AssignedSalesId,
            project.AssignedDesignerId);
    }

    private static ProjectParticipantAccess ToParticipantAccess(ProjectAreaDetailReadModel area)
    {
        return new ProjectParticipantAccess(
            area.CustomerId,
            area.AssignedSalesId,
            area.AssignedDesignerId);
    }

    private static bool IsNegative(decimal? value)
    {
        return value.HasValue && value.Value < 0;
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, CustomerRole, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ProjectStaffAssignment(Guid? AssignedSalesId, Guid? AssignedDesignerId);

    private readonly record struct ProjectParticipantAccess(
        Guid CustomerId,
        Guid? AssignedSalesId,
        Guid? AssignedDesignerId);
}
