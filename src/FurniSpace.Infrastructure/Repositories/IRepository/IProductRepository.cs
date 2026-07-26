using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<bool> ProductCodeExistsAsync(
        string productCode,
        CancellationToken cancellationToken = default);

    Task<ProductDetailReadModel?> GetDetailAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(
        int page,
        int limit,
        IReadOnlyCollection<int>? businessTypeIds = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        IReadOnlyCollection<int>? businessTypeIds = null,
        CancellationToken cancellationToken = default);

    Task<ProductCategoryReadModel?> GetCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default);

    Task<int> CountByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<ProductListItemReadModel?> GetSearchIndexItemAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ProductSearchResultReadModel> SearchPublicAsync(
        ProductSearchQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(
        Guid productId,
        int limit,
        CancellationToken cancellationToken = default);
}
