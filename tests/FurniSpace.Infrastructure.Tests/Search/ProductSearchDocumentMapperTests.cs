#nullable enable

using System;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.Search;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class ProductSearchDocumentMapperTests
{
    [Fact]
    public void IsIndexable_ReturnsTrueForActivePublicDefaultVersion()
    {
        var item = CreateListItem(ProductStatus.ACTIVE, ProductStatus.ACTIVE, isPublic: true);

        Assert.True(ProductSearchDocumentMapper.IsIndexable(item));
    }

    [Fact]
    public void IsIndexable_ReturnsFalseWhenProductInactive()
    {
        var item = CreateListItem(ProductStatus.INACTIVE, ProductStatus.ACTIVE, isPublic: true);

        Assert.False(ProductSearchDocumentMapper.IsIndexable(item));
    }

    [Fact]
    public void ToDocument_MapsDefaultVersionFields()
    {
        var item = CreateListItem(ProductStatus.ACTIVE, ProductStatus.ACTIVE, isPublic: true);

        var document = ProductSearchDocumentMapper.ToDocument(item);

        Assert.Equal(item.ProductId, document.ProductId);
        Assert.Equal("Oak", document.Material);
        Assert.Equal("Brown", document.Color);
        Assert.Equal(1200m, document.EstimatedPrice);
        Assert.True(document.IsPublic);
    }

    private static ProductListItemReadModel CreateListItem(
        ProductStatus productStatus,
        ProductStatus versionStatus,
        bool isPublic)
    {
        return new ProductListItemReadModel
        {
            ProductId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            CategoryName = "Tables",
            ProductCode = "TBL-001",
            ProductName = "Work Table",
            Description = "Solid wood table",
            Status = productStatus,
            DefaultVersion = new ProductVersionReadModel
            {
                ProductVersionId = Guid.NewGuid(),
                VersionCode = "V1",
                VersionName = "Standard",
                Material = "Oak",
                Color = "Brown",
                EstimatedPrice = 1200m,
                Status = versionStatus,
                IsPublic = isPublic,
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}
