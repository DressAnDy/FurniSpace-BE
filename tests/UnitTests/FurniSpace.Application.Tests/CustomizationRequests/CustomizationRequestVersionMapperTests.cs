#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class CustomizationRequestVersionMapperTests
{
    public CustomizationRequestVersionMapperTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public void ToDto_FromEntity_MapsVersionFieldsAndProductVersion()
    {
        var versionId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var version = new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = versionId,
            CustomizationRequestId = Guid.NewGuid(),
            ProductVersionId = productVersionId,
            VersionNo = 2,
            CreatedByDesignerId = Guid.NewGuid(),
            VersionTitle = "Walnut finish",
            DesignerNote = "Reinforced frame",
            Status = CustomizationVersionStatus.DRAFT,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var productVersion = new ProductVersion
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-001",
            VersionName = "Custom Chair",
            Material = "Walnut",
            DimensionUnit = "cm",
            Status = ProductStatus.ACTIVE
        };

        var dto = CustomizationRequestVersionMapper.ToDto(version, productVersion);

        Assert.Equal(versionId, dto.CustomizationRequestVersionId);
        Assert.Equal(2, dto.VersionNo);
        Assert.Equal("Walnut finish", dto.VersionTitle);
        Assert.Equal(CustomizationVersionStatus.DRAFT, dto.Status);
        Assert.Equal(productVersionId, dto.ProductVersion.ProductVersionId);
        Assert.Equal("Custom Chair", dto.ProductVersion.VersionName);
        Assert.Equal("Walnut", dto.ProductVersion.Material);
    }

    [Fact]
    public void ToDto_FromReadModel_MapsVersionFieldsAndProductVersion()
    {
        var versionId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var readModel = new CustomizationRequestVersionReadModel
        {
            CustomizationRequestVersionId = versionId,
            CustomizationRequestId = Guid.NewGuid(),
            ProductVersionId = productVersionId,
            VersionNo = 1,
            CreatedByDesignerId = Guid.NewGuid(),
            VersionTitle = "Oak option",
            Status = CustomizationVersionStatus.REVIEWING,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            SubmittedForReviewAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ProductVersion = new ProductVersion
            {
                ProductVersionId = productVersionId,
                ProductId = Guid.NewGuid(),
                VersionCode = "PV-002",
                VersionName = "Oak Chair",
                Material = "Oak",
                Status = ProductStatus.ACTIVE
            }
        };

        var dto = CustomizationRequestVersionMapper.ToDto(readModel);

        Assert.Equal(versionId, dto.CustomizationRequestVersionId);
        Assert.Equal(CustomizationVersionStatus.REVIEWING, dto.Status);
        Assert.Equal("Oak Chair", dto.ProductVersion.VersionName);
        Assert.Equal("Oak", dto.ProductVersion.Material);
    }
}
