using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Catalog;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Catalog;

public sealed class ProjectCatalogService : IProjectCatalogService
{
    private readonly ICatalogRepository _catalog;
    private readonly IProjectRepository _projects;
    private readonly IProjectFileRepository _files;

    public ProjectCatalogService(
        ICatalogRepository catalog,
        IProjectRepository projects,
        IProjectFileRepository files)
    {
        _catalog = catalog;
        _projects = projects;
        _files = files;
    }

    public async Task<ServiceResult<ProjectCatalogListResponseDto>> GetProductsAsync(
        Guid projectId,
        Guid currentUserId,
        string? role,
        ProjectCatalogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateDesignerAccessAsync(projectId, currentUserId, role, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<ProjectCatalogListResponseDto>.Failure(accessError);
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            return ServiceResult<ProjectCatalogListResponseDto>.Failure(
                Error.BadRequest(CatalogErrorCodes.CatalogFilterInvalid, "Invalid pagination parameters."));
        }

        query.ProjectId = projectId;
        var items = await _catalog.GetProjectCatalogAsync(query, cancellationToken);
        var total = await _catalog.CountProjectCatalogAsync(query, cancellationToken);
        var productIds = items.Select(item => item.ProductId).ToList();
        var thumbnails = await LoadProductThumbnailsAsync(productIds, cancellationToken);

        return ServiceResult<ProjectCatalogListResponseDto>.Success(
            new ProjectCatalogListResponseDto
            {
                Items = items.Select(item => ToListItem(item, thumbnails)).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = total
            },
            string.Empty);
    }

    public async Task<ServiceResult<ProjectCatalogProductDetailDto>> GetProductByIdAsync(
        Guid projectId,
        Guid productId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateDesignerAccessAsync(projectId, currentUserId, role, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<ProjectCatalogProductDetailDto>.Failure(accessError);
        }

        var item = await _catalog.GetProjectCatalogProductDetailAsync(projectId, productId, cancellationToken);
        if (item is null)
        {
            return ServiceResult<ProjectCatalogProductDetailDto>.Failure(
                Error.NotFound(
                    CatalogErrorCodes.CatalogProductNotEligible,
                    "Product is not eligible for this project."));
        }

        var thumbnails = await LoadProductThumbnailsAsync([productId], cancellationToken);
        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            [productId],
            customerVisibleOnly: true,
            cancellationToken);

        return ServiceResult<ProjectCatalogProductDetailDto>.Success(
            new ProjectCatalogProductDetailDto
            {
                ProductId = item.ProductId,
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                Description = item.Description,
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName,
                BusinessTypeIds = item.BusinessTypeIds,
                Thumbnail = thumbnails.GetValueOrDefault(productId),
                Files = ToCatalogFileList(files),
                EligibleVersions = item.EligibleVersions.Adapt<List<ProjectCatalogVersionSummaryDto>>()
            },
            string.Empty);
    }

    public async Task<ServiceResult<ProjectCatalogProductVersionDetailDto>> GetProductVersionByIdAsync(
        Guid projectId,
        Guid productVersionId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateDesignerAccessAsync(projectId, currentUserId, role, cancellationToken);
        if (accessError is not null)
        {
            return ServiceResult<ProjectCatalogProductVersionDetailDto>.Failure(accessError);
        }

        var version = await _catalog.GetProjectEligibleVersionDetailAsync(
            projectId,
            productVersionId,
            cancellationToken);
        if (version is null)
        {
            return ServiceResult<ProjectCatalogProductVersionDetailDto>.Failure(
                Error.NotFound(
                    CatalogErrorCodes.CatalogVersionNotEligible,
                    "Product version is not eligible for this project."));
        }

        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            [productVersionId],
            customerVisibleOnly: true,
            cancellationToken);

        var detail = version.Adapt<ProjectCatalogProductVersionDetailDto>();
        detail.Files = ToCatalogFileList(files);
        return ServiceResult<ProjectCatalogProductVersionDetailDto>.Success(detail, string.Empty);
    }

    private async Task<Error?> ValidateDesignerAccessAsync(
        Guid projectId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return Error.Unauthorized("UNAUTHORIZED", "Authenticated account id is required.");
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound(CatalogErrorCodes.ProjectNotFound, "Project not found.");
        }

        if (role != ProjectAssignmentAccessEvaluator.AdminRole &&
            (role != ProjectAssignmentAccessEvaluator.DesignerRole ||
             project.AssignedDesignerId != currentUserId))
        {
            return Error.Forbidden(CatalogErrorCodes.DesignerNotAssigned, "Designer is not assigned to this project.");
        }

        return null;
    }

    private async Task<Dictionary<Guid, CatalogFileDto>> LoadProductThumbnailsAsync(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            productIds,
            customerVisibleOnly: true,
            cancellationToken);

        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly: true))
            .GroupBy(file => file.ReferenceId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var mapped = group.Adapt<List<CatalogFileDto>>();
                    return mapped[0];
                });
    }

    private static ProjectCatalogProductItemDto ToListItem(
        ProjectCatalogProductListItemReadModel item,
        IReadOnlyDictionary<Guid, CatalogFileDto> thumbnails)
    {
        return new ProjectCatalogProductItemDto
        {
            ProductId = item.ProductId,
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            BusinessTypeIds = item.BusinessTypeIds,
            Thumbnail = thumbnails.GetValueOrDefault(item.ProductId),
            EligibleVersionCount = item.EligibleVersions.Count,
            EligibleVersions = item.EligibleVersions.Adapt<List<ProjectCatalogVersionSummaryDto>>()
        };
    }

    private static List<CatalogFileDto> ToCatalogFileList(IEnumerable<CatalogFileReadModel> files)
    {
        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly: true))
            .Adapt<List<CatalogFileDto>>();
    }
}
