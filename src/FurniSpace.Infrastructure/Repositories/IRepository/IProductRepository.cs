using FurniSpace.Domain.Entities;
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
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

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
}

public sealed class ProductCategoryReadModel
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public sealed class ProductListItemReadModel
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductType { get; set; }
    public string? Status { get; set; }
    public ProductVersionReadModel? DefaultVersion { get; set; }
}

public sealed class ProductDetailReadModel
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductType { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<ProductVersionReadModel> Versions { get; set; } = [];
}

public sealed class ProductVersionReadModel
{
    public Guid ProductVersionId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public string? VersionType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsPublic { get; set; }
    public bool? IsProjectSpecific { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
