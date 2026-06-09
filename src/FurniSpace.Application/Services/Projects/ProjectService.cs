using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectService : IProjectService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string SalesRole = "SALES";

    private readonly IProjectRepository _projects;

    public ProjectService(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<ServiceResult<ProjectDto>> CreateAsync(
        Guid currentUserId,
        CreateProjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectDto>.Unauthorized("Authenticated account id is required.");
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
        await _projects.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectDto>.Created(
            project.Adapt<ProjectDto>(),
            "Project request submitted successfully.");
    }

    public async Task<ServiceResult<ProjectListResponseDto>> GetListAsync(
        Guid currentUserId,
        ProjectListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectListResponseDto>.Unauthorized("Authenticated account id is required.");
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
            return ServiceResult<ProjectDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectDto>.Unauthorized("Authenticated account id is required.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectDto>.NotFound("Project not found.");
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
            return ServiceResult<ProjectSalesAssignmentDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.Unauthorized("Authenticated account id is required.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAssignSales(roleName))
        {
            return ServiceResult<ProjectSalesAssignmentDto>.Forbidden("Only sales or admin accounts can accept project requests.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectSalesAssignmentDto>.NotFound("Project not found.");
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

        await _projects.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectSalesAssignmentDto>.Success(
            new ProjectSalesAssignmentDto
            {
                ProjectId = project.ProjectId,
                AssignedSalesId = project.AssignedSalesId,
                Status = project.Status,
                SalesAssignedAt = project.SalesAssignedAt
            },
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
            return ServiceResult<ProjectInformationRequestDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectInformationRequestDto>.Unauthorized("Authenticated account id is required.");
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
            return ServiceResult<ProjectInformationRequestDto>.NotFound("Project not found.");
        }

        if (!IsAdmin(roleName) && project.AssignedSalesId != currentUserId)
        {
            return ServiceResult<ProjectInformationRequestDto>.Forbidden("You do not have access to request more information for this project.");
        }

        var requestedAt = DateTime.UtcNow;
        project.Status = ProjectStatus.NEED_BASIC_INFORMATION;
        project.UpdatedAt = requestedAt;

        await _projects.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectInformationRequestDto>.Success(
            new ProjectInformationRequestDto
            {
                ProjectId = project.ProjectId,
                Status = project.Status,
                RequestedAt = requestedAt
            },
            "More information requested successfully.");
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
            return project.AssignedSalesId == currentUserId;
        }

        if (string.Equals(roleName, "DESIGNER", StringComparison.OrdinalIgnoreCase))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return false;
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
