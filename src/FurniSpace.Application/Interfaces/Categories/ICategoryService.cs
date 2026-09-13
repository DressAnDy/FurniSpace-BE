using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Categories;

namespace FurniSpace.Application.Interfaces.Categories;

public interface ICategoryService
{
    Task<ServiceResult<CategoryDto>> CreateAsync(
        CreateCategoryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CategoryDto>> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CategoryListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);
}
