#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class ApprovedProductVersionSummaryMapperTests
{
    [Fact]
    public void ToDto_FromProductVersionDetailReadModel_MapsFieldsAndProjectId()
    {
        var projectId = Guid.NewGuid();
        var version = new ProductVersionDetailReadModel
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductName = "Cafe Chair",
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Material = "Oak",
            Color = "Natural",
            Width = 50m,
            Height = 80m,
            Depth = 45m,
            EstimatedPrice = 1700000m,
            IsDefault = false,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE
        };

        var dto = ApprovedProductVersionSummaryMapper.ToDto(version, projectId);

        Assert.Equal(version.ProductVersionId, dto.ProductVersionId);
        Assert.Equal(projectId, dto.ProjectId);
        Assert.Equal("PV-PRJ-000001-CUST-001", dto.VersionCode);
        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, dto.VersionType);
        Assert.False(dto.IsPublic);
        Assert.True(dto.IsProjectSpecific);
    }
}
