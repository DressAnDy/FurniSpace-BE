using FurniSpace.Application.Common;
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
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string ProjectAreaNotFoundMessage = "Project area not found.";

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
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.Unauthorized();
        }

        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Project area id is required.");
        }

        var detail = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanViewProjectArea(detail, currentUserId, role))
        {
            return ServiceResult<ProjectAreaDto>.Forbidden("You do not have access to this project area.");
        }

        return ServiceResult<ProjectAreaDto>.Success(
            detail.Adapt<ProjectAreaDto>(),
            "Project area retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectAreaDto>> UpdateAsync(
        Guid projectAreaId,
        Guid currentUserId,
        UpdateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.Unauthorized();
        }

        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Project area id is required.");
        }

        var detail = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageProjectArea(detail, currentUserId, role))
        {
            return ServiceResult<ProjectAreaDto>.Forbidden("You do not have access to update this project area.");
        }

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
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
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
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.Unauthorized();
        }

        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaDto>.BadRequest("Project area id is required.");
        }

        var detail = await _areas.GetDetailAsync(projectAreaId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageProjectArea(detail, currentUserId, role))
        {
            return ServiceResult<ProjectAreaDto>.Forbidden("You do not have access to cancel this project area.");
        }

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
            return ServiceResult<ProjectAreaDto>.Failure(
                Error.NotFound(ProjectAreaErrorCodes.ProjectAreaNotFound, ProjectAreaNotFoundMessage));
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
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return false;
    }

    private static bool CanManageProjectArea(
        ProjectAreaDetailReadModel area,
        Guid currentUserId,
        string? roleName)
    {
        return CanManageProjectAreas(
            new ProjectDetailReadModel
            {
                ProjectId = area.ProjectId,
                AssignedSalesId = area.AssignedSalesId,
                AssignedDesignerId = area.AssignedDesignerId
            },
            currentUserId,
            roleName);
    }

    private static bool CanViewProjectAreas(
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

    private static bool CanViewProjectArea(
        ProjectAreaDetailReadModel area,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return area.CustomerId == currentUserId;
        }

        if (string.Equals(roleName, SalesRole, StringComparison.OrdinalIgnoreCase))
        {
            if (!area.AssignedSalesId.HasValue && !area.AssignedDesignerId.HasValue)
            {
                return true;
            }

            return area.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, DesignerRole, StringComparison.OrdinalIgnoreCase))
        {
            return area.AssignedDesignerId == currentUserId;
        }

        return false;
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

}
