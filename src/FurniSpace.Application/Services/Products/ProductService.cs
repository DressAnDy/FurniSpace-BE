using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;

    public ProductService(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductDto>.BadRequest(errors);
        }

        if (await _products.GetCategoryAsync(request.CategoryId, cancellationToken) is null)
        {
            return ServiceResult<ProductDto>.BadRequest("Category does not exist.");
        }

        var productCode = NormalizeOptional(request.ProductCode);
        if (productCode is not null &&
            await _products.ProductCodeExistsAsync(productCode, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict("Product code already exists.");
        }

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            ProductCode = productCode,
            ProductName = request.ProductName.Trim(),
            Description = NormalizeOptional(request.Description)
        };

        await _products.AddAsync(product, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        product.Status ??= ProductStatus.ACTIVE;

        return ServiceResult<ProductDto>.Created(product.Adapt<ProductDto>(), "Product master created successfully.");
    }

    public async Task<ServiceResult<ProductDetailDto>> GetByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductDetailDto>.BadRequest("Product id is required.");
        }

        var product = await _products.GetDetailAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductDetailDto>.NotFound("Product not found.");
        }

        return ServiceResult<ProductDetailDto>.Success(ToDetailDto(product), string.Empty);
    }

    public async Task<ServiceResult<ProductListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePagination(page, limit);
        if (validationError is not null)
        {
            return ServiceResult<ProductListResponseDto>.BadRequest(validationError);
        }

        var products = await _products.GetPublicListAsync(page, limit, cancellationToken);
        var total = await _products.CountAsync(cancellationToken);
        var response = new ProductListResponseDto
        {
            Items = products.Adapt<List<ProductListItemDto>>(),
            Page = page,
            Limit = limit,
            Total = total
        };

        return ServiceResult<ProductListResponseDto>.Success(response, string.Empty);
    }

    public async Task<ServiceResult<ProductByCategoryResponseDto>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
        {
            return ServiceResult<ProductByCategoryResponseDto>.BadRequest("Category id is required.");
        }

        var validationError = ValidatePagination(page, limit);
        if (validationError is not null)
        {
            return ServiceResult<ProductByCategoryResponseDto>.BadRequest(validationError);
        }

        var category = await _products.GetCategoryAsync(categoryId, cancellationToken);
        if (category is null)
        {
            return ServiceResult<ProductByCategoryResponseDto>.NotFound("Category not found.");
        }

        var products = await _products.GetPublicListByCategoryAsync(
            categoryId,
            page,
            limit,
            includeDefaultVersion,
            cancellationToken);
        var total = await _products.CountByCategoryAsync(categoryId, cancellationToken);
        var response = new ProductByCategoryResponseDto
        {
            Category = category.Adapt<ProductCategorySummaryDto>(),
            Items = products.Adapt<List<ProductListItemDto>>(),
            Page = page,
            Limit = limit,
            Total = total
        };

        return ServiceResult<ProductByCategoryResponseDto>.Success(response, string.Empty);
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

    private static List<string> ValidateCreateRequest(CreateProductRequestDto request)
    {
        var errors = new List<string>();
        if (request.CategoryId == Guid.Empty)
        {
            errors.Add("Category id is required.");
        }

        if (request.ProductCode?.Trim().Length > 50)
        {
            errors.Add("Product code must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            errors.Add("Product name is required.");
        }
        else if (request.ProductName.Trim().Length > 150)
        {
            errors.Add("Product name must not exceed 150 characters.");
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ProductDetailDto ToDetailDto(ProductDetailReadModel product)
    {
        var defaultVersion = product.Versions
            .Where(IsUsablePublicVersion)
            .OrderByDescending(version => version.IsDefault == true)
            .ThenBy(version => version.CreatedAt)
            .ThenBy(version => version.ProductVersionId)
            .FirstOrDefault();

        var dto = product.Adapt<ProductDetailDto>();
        dto.DefaultVersion = defaultVersion?.Adapt<ProductVersionSummaryDto>();
        return dto;
    }

    private static bool IsUsablePublicVersion(ProductVersionReadModel version)
    {
        return version.Status == ProductStatus.ACTIVE &&
            version.IsPublic == true;
    }
}
