using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.ProjectShowcases;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using static FurniSpace.Application.Constants.ProjectShowcases.ProjectShowcaseServiceConstants;

namespace FurniSpace.Application.Services.ProjectShowcases;

public sealed partial class ProjectShowcaseService : IProjectShowcaseService
{
    private static readonly FileType[] AllowedShowcaseFileTypes =
    [
        FileType.PORTFOLIO_IMAGE,
        FileType.REVIEW_IMAGE,
        FileType.DELIVERY_PHOTO,
        FileType.SPACE_IMAGE,
        FileType.REFERENCE_IMAGE,
        FileType.PROPOSAL_PREVIEW
    ];

    private static readonly Regex SlugCleanupPattern = new(
        "[^a-z0-9]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly IProjectRepository _projects;
    private readonly IProjectShowcaseRepository _showcases;
    private readonly IProjectReviewRepository _reviews;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectShowcaseService(
        IProjectRepository projects,
        IProjectShowcaseRepository showcases,
        IProjectReviewRepository reviews,
        IProjectFileRepository files,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _showcases = showcases;
        _reviews = reviews;
        _files = files;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectShowcaseRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectShowcaseDto>.BadRequest("Project id and current user are required.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageContent(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(ManageForbiddenMessage);
        }

        if (await _showcases.ProjectHasShowcaseAsync(projectId, cancellationToken))
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.Conflict(ProjectShowcaseErrorCodes.AlreadyExists, "This project already has a showcase."));
        }

        var now = DateTime.UtcNow;
        var title = NormalizeRequiredText(request?.Title) ?? project.ProjectName.Trim();
        var slug = await EnsureUniqueSlugAsync(GenerateSlug(title, projectId), null, cancellationToken);
        var showcase = new ProjectShowcase
        {
            ProjectShowcaseId = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Slug = slug,
            Summary = NormalizeOptionalText(request?.Summary),
            Description = NormalizeOptionalText(request?.Description),
            Status = ProjectShowcaseStatus.DRAFT,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _showcases.AddAsync(showcase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Created(
            await BuildDtoAsync(showcase.ProjectShowcaseId, cancellationToken),
            CreatedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectShowcaseDto>.BadRequest("Project id and current user are required.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanView(null, project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(ViewForbiddenMessage);
        }

        var showcase = await _showcases.GetByProjectIdAsync(projectId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var detail = await _showcases.GetDetailAsync(showcase.ProjectShowcaseId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        return ServiceResult<ProjectShowcaseDto>.Success(detail.Adapt<ProjectShowcaseDto>(), RetrievedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> UpdateAsync(
        Guid showcaseId,
        Guid currentUserId,
        UpdateProjectShowcaseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageContent(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, "Archived showcases cannot be edited."));
        }

        if (request.Title is not null)
        {
            var title = NormalizeRequiredText(request.Title);
            if (title is null)
            {
                return ServiceResult<ProjectShowcaseDto>.BadRequest("Title cannot be empty.");
            }

            showcase.Title = title;
        }

        if (request.Summary is not null)
        {
            showcase.Summary = NormalizeOptionalText(request.Summary);
        }

        if (request.Description is not null)
        {
            showcase.Description = NormalizeOptionalText(request.Description);
        }

        if (request.Slug is not null)
        {
            var slug = NormalizeSlug(request.Slug);
            if (slug is null)
            {
                return ServiceResult<ProjectShowcaseDto>.BadRequest("Slug is invalid.");
            }

            if (await _showcases.SlugExistsAsync(slug, showcase.ProjectShowcaseId, cancellationToken))
            {
                return ServiceResult<ProjectShowcaseDto>.Failure(
                    Error.Conflict(ProjectShowcaseErrorCodes.SlugDuplicate, "Showcase slug already exists."));
            }

            showcase.Slug = slug;
        }

        if (request.FeaturedReviewId.HasValue)
        {
            var featuredReviewError = await ValidateFeaturedReviewAsync<ProjectShowcaseDto>(
                showcase.ProjectId,
                request.FeaturedReviewId,
                cancellationToken);
            if (featuredReviewError is not null)
            {
                return featuredReviewError;
            }

            showcase.FeaturedReviewId = request.FeaturedReviewId;
        }

        showcase.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcase.ProjectShowcaseId, cancellationToken),
            UpdatedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> SubmitAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageContent(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(SubmitForbiddenMessage);
        }

        if (showcase.Status != ProjectShowcaseStatus.DRAFT)
        {
            return InvalidTransition<ProjectShowcaseDto>();
        }

        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        showcase.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcase.ProjectShowcaseId, cancellationToken),
            SubmittedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> PublishAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsAdmin(roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(PublishForbiddenMessage);
        }

        if (showcase.Status != ProjectShowcaseStatus.PENDING_REVIEW)
        {
            return InvalidTransition<ProjectShowcaseDto>();
        }

        if (project.Status != ProjectStatus.COMPLETED)
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.ProjectNotCompleted,
                    "Project must be COMPLETED before publishing a showcase."));
        }

        var publishValidation = await ValidatePublishRequirementsAsync(showcase, cancellationToken);
        if (publishValidation is not null)
        {
            return publishValidation;
        }

        var now = DateTime.UtcNow;
        showcase.Status = ProjectShowcaseStatus.PUBLISHED;
        showcase.ApprovedBy = currentUserId;
        showcase.PublishedBy = currentUserId;
        showcase.ApprovedAt = now;
        showcase.PublishedAt = now;
        showcase.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcase.ProjectShowcaseId, cancellationToken),
            PublishedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> ArchiveAsync(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsAdmin(roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(PublishForbiddenMessage);
        }

        if (showcase.Status != ProjectShowcaseStatus.PUBLISHED)
        {
            return InvalidTransition<ProjectShowcaseDto>();
        }

        var now = DateTime.UtcNow;
        showcase.Status = ProjectShowcaseStatus.ARCHIVED;
        showcase.ArchivedAt = now;
        showcase.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcase.ProjectShowcaseId, cancellationToken),
            ArchivedMessage);
    }

    public async Task<ServiceResult<PublicShowcaseListResponseDto>> GetPublicListAsync(
        PublicShowcaseQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var items = await _showcases.GetPublishedPagedAsync(page, pageSize, cancellationToken);
        var total = await _showcases.CountPublishedAsync(cancellationToken);

        return ServiceResult<PublicShowcaseListResponseDto>.Success(
            new PublicShowcaseListResponseDto
            {
                Items = items.Adapt<List<PublicShowcaseListItemDto>>(),
                Page = page,
                PageSize = pageSize,
                Total = total
            },
            RetrievedMessage);
    }

    public async Task<ServiceResult<PublicShowcaseDetailDto>> GetPublicBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (normalizedSlug is null)
        {
            return ServiceResult<PublicShowcaseDetailDto>.BadRequest("Slug is required.");
        }

        var detail = await _showcases.GetPublishedBySlugAsync(normalizedSlug, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<PublicShowcaseDetailDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        return ServiceResult<PublicShowcaseDetailDto>.Success(detail.Adapt<PublicShowcaseDetailDto>(), RetrievedMessage);
    }

    private async Task<ProjectShowcaseDto> BuildDtoAsync(Guid showcaseId, CancellationToken cancellationToken)
    {
        var detail = await _showcases.GetDetailAsync(showcaseId, cancellationToken);
        return detail?.Adapt<ProjectShowcaseDto>() ?? new ProjectShowcaseDto();
    }

    private async Task<ServiceResult<T>?> ValidateFeaturedReviewAsync<T>(
        Guid projectId,
        Guid? featuredReviewId,
        CancellationToken cancellationToken)
    {
        if (!featuredReviewId.HasValue)
        {
            return null;
        }

        var review = await _reviews.GetByIdAsync(featuredReviewId.Value, cancellationToken);
        if (review is null || review.ProjectId != projectId)
        {
            return ServiceResult<T>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.FeaturedReviewInvalid,
                    "Featured review must belong to the same project."));
        }

        return null;
    }

    private async Task<ServiceResult<ProjectShowcaseDto>?> ValidatePublishRequirementsAsync(
        ProjectShowcase showcase,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(showcase.Title) || string.IsNullOrWhiteSpace(showcase.Summary))
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.PublishRequirementsNotMet,
                    "Published showcases require title and summary."));
        }

        if (!await _showcases.HasCoverMediaAsync(showcase.ProjectShowcaseId, cancellationToken))
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.PublishRequirementsNotMet,
                    "Published showcases require one cover media item."));
        }

        return null;
    }

    private async Task<string> EnsureUniqueSlugAsync(
        string baseSlug,
        Guid? excludeShowcaseId,
        CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 1;
        while (await _showcases.SlugExistsAsync(slug, excludeShowcaseId, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static string GenerateSlug(string source, Guid projectId)
    {
        var slug = SlugCleanupPattern.Replace(
            source.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD),
            "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = $"project-{projectId:N}".ToLowerInvariant();
        }

        return slug.Length > 200 ? slug[..200].Trim('-') : slug;
    }

    private static string? NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var slug = SlugCleanupPattern.Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeRequiredText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ServiceResult<T> InvalidTransition<T>()
    {
        return ServiceResult<T>.Failure(
            Error.BadRequest(
                ProjectShowcaseErrorCodes.InvalidStatusTransition,
                "Project showcase status transition is not allowed."));
    }

    private static bool CanView(
        ProjectShowcaseDetailReadModel? detail,
        Project? project,
        Guid currentUserId,
        string? roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        var customerId = detail?.CustomerId ?? project?.CustomerId;
        var salesId = detail?.AssignedSalesId ?? project?.AssignedSalesId;
        var designerId = detail?.AssignedDesignerId ?? project?.AssignedDesignerId;

        if (IsSales(roleName))
        {
            return salesId == currentUserId;
        }

        if (IsDesigner(roleName))
        {
            return designerId == currentUserId;
        }

        return false;
    }

    private static bool CanManageContent(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) || (IsSales(roleName) && project.AssignedSalesId == currentUserId);
    }

    internal static bool CanManageMedia(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (IsSales(roleName) && project.AssignedSalesId == currentUserId) ||
            (IsDesigner(roleName) && project.AssignedDesignerId == currentUserId);
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSales(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);
    }
}
