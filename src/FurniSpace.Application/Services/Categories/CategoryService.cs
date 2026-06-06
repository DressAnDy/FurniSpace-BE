using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Application.Interfaces.Categories;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Categories;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;

    public CategoryService(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<ServiceResult<CategoryDto>> CreateAsync(
        CreateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCategoryRequest(request.CategoryName);
        if (errors.Count > 0)
        {
            return ServiceResult<CategoryDto>.BadRequest(errors);
        }

        var categoryName = request.CategoryName.Trim();
        if (await _categories.NameExistsAsync(categoryName, cancellationToken))
        {
            return ServiceResult<CategoryDto>.Conflict("Category name already exists.");
        }

        var category = new Domain.Entities.Category
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = categoryName,
            Description = NormalizeOptional(request.Description),
            Status = ProductStatus.ACTIVE
        };

        await _categories.AddAsync(category, cancellationToken);
        await _categories.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryDto>.Created(
            category.Adapt<CategoryDto>(),
            "Category created successfully.");
    }

    public async Task<ServiceResult<CategoryDto>> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
        {
            return ServiceResult<CategoryDto>.BadRequest("Category id is required.");
        }

        var errors = ValidateCategoryRequest(request.CategoryName);
        if (errors.Count > 0)
        {
            return ServiceResult<CategoryDto>.BadRequest(errors);
        }

        var category = await _categories.GetByIdAsync(categoryId, cancellationToken);
        if (category is null)
        {
            return ServiceResult<CategoryDto>.NotFound("Category not found.");
        }

        var categoryName = request.CategoryName.Trim();
        if (await _categories.NameExistsAsync(categoryName, categoryId, cancellationToken))
        {
            return ServiceResult<CategoryDto>.Conflict("Category name already exists.");
        }

        category.CategoryName = categoryName;
        category.Description = NormalizeOptional(request.Description);
        category.Status ??= ProductStatus.ACTIVE;

        await _categories.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryDto>.Success(
            category.Adapt<CategoryDto>(),
            "Category updated successfully.");
    }

    public async Task<ServiceResult<CategoryListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return ServiceResult<CategoryListResponseDto>.BadRequest("Page must be greater than zero.");
        }

        if (limit is < 1 or > 100)
        {
            return ServiceResult<CategoryListResponseDto>.BadRequest("Limit must be between 1 and 100.");
        }

        var items = await _categories.GetPagedAsync(page, limit, cancellationToken);
        var total = await _categories.CountAsync(cancellationToken);
        var response = new CategoryListResponseDto
        {
            Items = items.Adapt<List<CategoryDto>>(),
            Page = page,
            Limit = limit,
            Total = total
        };

        return ServiceResult<CategoryListResponseDto>.Success(response, string.Empty);
    }

    private static List<string> ValidateCategoryRequest(string categoryName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            errors.Add("Category name is required.");
        }
        else if (categoryName.Trim().Length > 100)
        {
            errors.Add("Category name must not exceed 100 characters.");
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
