using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.DTOs.Products;

namespace FurniSpace.Infrastructure.Search;

public static class ProductSearchDocumentMapper
{
    public static bool IsIndexable(ProductListItemReadModel item)
    {
        if (item.Status is not null and not ProductStatus.ACTIVE)
        {
            return false;
        }

        var version = item.DefaultVersion;
        return version is not null &&
            version.Status == ProductStatus.ACTIVE &&
            version.IsPublic == true;
    }

    public static ProductSearchDocument ToDocument(ProductListItemReadModel item)
    {
        var version = item.DefaultVersion
            ?? throw new InvalidOperationException("Product search document requires a default version.");

        return new ProductSearchDocument
        {
            ProductId = item.ProductId,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            Description = item.Description,
            Material = version.Material,
            Color = version.Color,
            Width = version.Width,
            Height = version.Height,
            Depth = version.Depth,
            EstimatedPrice = version.EstimatedPrice,
            Status = item.Status?.ToString(),
            IsPublic = version.IsPublic == true,
            CreatedAt = version.CreatedAt ?? DateTime.UtcNow
        };
    }
}
