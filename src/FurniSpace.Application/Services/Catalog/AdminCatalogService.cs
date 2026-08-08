using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Interfaces.Catalog;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Catalog;

public sealed class AdminCatalogService : IAdminCatalogService
{
    private static readonly HashSet<string> AllowedSortFields =
    [
        "createdat",
        "updatedat",
        "productname",
        "productcode"
    ];

    private readonly ICatalogRepository _catalog;
    private readonly IProductRepository _products;
    private readonly IBusinessTypeRepository _businessTypes;

    public AdminCatalogService(
        ICatalogRepository catalog,
        IProductRepository products,
        IBusinessTypeRepository businessTypes)
    {
        _catalog = catalog;
        _products = products;
        _businessTypes = businessTypes;
    }

    public async Task<ServiceResult<AdminCatalogListResponseDto>> GetProductsAsync(
        AdminCatalogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateQuery(query);
        if (validation is not null)
        {
            return validation;
        }

        if (query.CategoryId.HasValue &&
            await _products.GetCategoryAsync(query.CategoryId.Value, cancellationToken) is null)
        {
            return ServiceResult<AdminCatalogListResponseDto>.Failure(
                Error.NotFound(CatalogErrorCodes.CategoryNotFound, "Category not found."));
        }

        if (query.BusinessTypeId.HasValue &&
            await _businessTypes.GetByIdAsync(query.BusinessTypeId.Value, cancellationToken) is null)
        {
            return ServiceResult<AdminCatalogListResponseDto>.Failure(
                Error.NotFound(CatalogErrorCodes.BusinessTypeNotFound, "Business type not found."));
        }

        var readQuery = query.Adapt<AdminCatalogQueryReadModel>();
        var items = await _catalog.GetAdminCatalogAsync(readQuery, cancellationToken);
        var total = await _catalog.CountAdminCatalogAsync(readQuery, cancellationToken);

        return ServiceResult<AdminCatalogListResponseDto>.Success(
            new AdminCatalogListResponseDto
            {
                Items = items.Select(ToAdminItem).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = total
            },
            string.Empty);
    }

    private static ServiceResult<AdminCatalogListResponseDto>? ValidateQuery(AdminCatalogQueryDto query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            return ServiceResult<AdminCatalogListResponseDto>.Failure(
                Error.BadRequest(CatalogErrorCodes.CatalogFilterInvalid, "Invalid pagination parameters."));
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) &&
            !AllowedSortFields.Contains(query.SortBy.Trim().ToLowerInvariant()))
        {
            return ServiceResult<AdminCatalogListResponseDto>.Failure(
                Error.BadRequest(CatalogErrorCodes.CatalogSortInvalid, "Invalid sort field."));
        }

        if (!string.IsNullOrWhiteSpace(query.SortDirection) &&
            !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<AdminCatalogListResponseDto>.Failure(
                Error.BadRequest(CatalogErrorCodes.CatalogSortInvalid, "Invalid sort direction."));
        }

        return null;
    }

    private static AdminCatalogProductItemDto ToAdminItem(AdminCatalogProductListItemReadModel item)
    {
        return new AdminCatalogProductItemDto
        {
            ProductId = item.ProductId,
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            BusinessTypeIds = item.BusinessTypeIds,
            Status = item.Status,
            TotalVersionCount = item.TotalVersionCount,
            ActiveVersionCount = item.ActiveVersionCount,
            InactiveVersionCount = item.InactiveVersionCount,
            ArchivedVersionCount = item.ArchivedVersionCount,
            DefaultVersionSummary = item.DefaultVersionId.HasValue
                ? new AdminCatalogDefaultVersionSummaryDto
                {
                    ProductVersionId = item.DefaultVersionId.Value,
                    VersionCode = item.DefaultVersionCode ?? string.Empty,
                    VersionName = item.DefaultVersionName ?? string.Empty,
                    Status = item.DefaultVersionStatus,
                    EstimatedPrice = item.DefaultVersionEstimatedPrice,
                    DefaultTaxRate = item.DefaultVersionDefaultTaxRate
                }
                : null,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
