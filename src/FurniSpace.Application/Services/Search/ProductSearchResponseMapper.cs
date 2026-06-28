using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search.Documents;

namespace FurniSpace.Application.Services.Search;

public static class ProductSearchResponseMapper
{
    public static ProductListItemDto ToListItem(ProductSearchDocument document)
    {
        _ = Enum.TryParse<ProductStatus>(document.Status, ignoreCase: true, out var status);

        return new ProductListItemDto
        {
            ProductId = document.ProductId,
            CategoryId = document.CategoryId,
            CategoryName = document.CategoryName,
            ProductCode = document.ProductCode,
            ProductName = document.ProductName,
            Description = document.Description,
            Status = document.Status is null ? null : status,
            DefaultVersion = new ProductVersionSummaryDto
            {
                Material = document.Material,
                Color = document.Color,
                Width = document.Width,
                Height = document.Height,
                Depth = document.Depth,
                EstimatedPrice = document.EstimatedPrice,
                Status = ProductStatus.ACTIVE,
                IsPublic = document.IsPublic
            }
        };
    }
}
