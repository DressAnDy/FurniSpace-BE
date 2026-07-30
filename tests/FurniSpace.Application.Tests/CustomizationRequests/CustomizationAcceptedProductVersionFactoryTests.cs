#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class CustomizationAcceptedProductVersionFactoryTests
{
    [Fact]
    public void LinkToCustomizationRequest_SetsApprovedProductVersionId()
    {
        var request = new CustomizationRequest { Status = CustomizationStatus.DESIGN_REVIEWING };
        var productVersion = new ProductVersion { ProductVersionId = Guid.NewGuid() };

        CustomizationAcceptedProductVersionFactory.LinkToCustomizationRequest(request, productVersion);

        Assert.Equal(productVersion.ProductVersionId, request.ApprovedProductVersionId);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, request.Status);
    }

    [Fact]
    public void MarkAccepted_SetsAcceptedStatusAndTimestamp()
    {
        var request = new CustomizationRequest
        {
            Status = CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL
        };

        CustomizationAcceptedProductVersionFactory.MarkAccepted(request);

        Assert.Equal(CustomizationStatus.ACCEPTED, request.Status);
        Assert.NotNull(request.CustomerAcceptedAt);
    }

    [Fact]
    public void CreateFromDesignerRequest_PrefersRequestValuesThenCustomizationThenSource()
    {
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var requestDto = new CreateCustomizationProductVersionRequestDto
        {
            VersionName = "Cafe Chair Custom",
            Material = "Walnut",
            Width = 65m,
            EstimatedPrice = 3200000m,
            DimensionUnit = "CM"
        };
        var customizationRequest = new CustomizationRequest
        {
            ProjectId = projectId,
            RequestedMaterial = "Oak",
            RequestedColor = "Brown",
            RequestedHeight = 90m,
            EstimatedAdditionalCost = 200000m
        };
        var sourceVersion = new ProductVersion
        {
            ProductId = productId,
            Material = "Pine",
            Color = "Natural",
            Width = 50m,
            Height = 80m,
            Depth = 40m,
            DimensionUnit = "mm",
            EstimatedPrice = 1000000m
        };

        var version = CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
            requestDto,
            customizationRequest,
            sourceVersion,
            "PRJ-000001",
            1,
            "Cafe Chair Custom",
            null);

        Assert.Equal("Cafe Chair Custom", version.VersionName);
        Assert.Equal("Walnut", version.Material);
        Assert.Equal("Brown", version.Color);
        Assert.Equal(65m, version.Width);
        Assert.Equal(90m, version.Height);
        Assert.Equal(40m, version.Depth);
        Assert.Equal("cm", version.DimensionUnit);
        Assert.Equal(3200000m, version.EstimatedPrice);
        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, version.VersionType);
        Assert.True(version.IsProjectSpecific);
        Assert.False(version.IsPublic);
        Assert.False(version.IsDefault);
        Assert.Equal(projectId, version.ProjectId);
        Assert.Equal(productId, version.ProductId);
    }

    [Fact]
    public void ToSummaryDto_MapsProductVersionFields()
    {
        var versionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var version = new ProductVersion
        {
            ProductVersionId = versionId,
            ProductId = productId,
            ProjectId = projectId,
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

        ApprovedProductVersionSummaryDto dto = CustomizationAcceptedProductVersionFactory.ToSummaryDto(version);

        Assert.Equal(versionId, dto.ProductVersionId);
        Assert.Equal(productId, dto.ProductId);
        Assert.Equal(projectId, dto.ProjectId);
        Assert.Equal("PV-PRJ-000001-CUST-001", dto.VersionCode);
        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, dto.VersionType);
        Assert.Equal(1700000m, dto.EstimatedPrice);
        Assert.False(dto.IsPublic);
        Assert.True(dto.IsProjectSpecific);
    }

    [Fact]
    public void ValidateVersionName_WhenMissing_ReturnsError()
    {
        var error = CustomizationAcceptedProductVersionFactory.ValidateVersionName(null);

        Assert.Equal("Version name is required.", error);
    }

    [Fact]
    public void ValidateVersionName_WhenTooLong_ReturnsError()
    {
        var error = CustomizationAcceptedProductVersionFactory.ValidateVersionName(new string('A', 151));

        Assert.Contains("150", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateVersionCode_WhenTooLong_ReturnsError()
    {
        var error = CustomizationAcceptedProductVersionFactory.ValidateVersionCode(new string('A', 51));

        Assert.Contains("50", error, StringComparison.Ordinal);
    }

    [Fact]
    public void IsValidDimensionUnit_AcceptsSupportedUnits()
    {
        Assert.True(CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit("cm"));
        Assert.True(CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit("MM"));
        Assert.False(CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit("inch"));
    }

    [Fact]
    public void CreateFromDesignerRequest_GeneratesVersionCodeWhenMissing()
    {
        var customizationRequest = new CustomizationRequest
        {
            ProjectId = Guid.NewGuid(),
            EstimatedAdditionalCost = 100000m
        };
        var sourceVersion = new ProductVersion
        {
            ProductId = Guid.NewGuid(),
            EstimatedPrice = 900000m,
            DimensionUnit = "cm"
        };

        var version = CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
            new CreateCustomizationProductVersionRequestDto
            {
                VersionName = "Auto Code Version",
                DimensionUnit = "cm"
            },
            customizationRequest,
            sourceVersion,
            "PRJ-000001",
            2,
            "Auto Code Version",
            null);

        Assert.Equal("PV-PRJ-000001-CUST-002", version.VersionCode);
        Assert.Equal(1000000m, version.EstimatedPrice);
    }

    [Fact]
    public void ToCreateResponse_MapsCustomizationAndProductVersion()
    {
        var requestId = Guid.NewGuid();
        var sourceVersionId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        var request = new CustomizationRequest
        {
            CustomizationRequestId = requestId,
            ProjectId = projectId,
            ProductVersionId = sourceVersionId,
            Status = CustomizationStatus.DESIGN_REVIEWING
        };
        var version = new ProductVersion
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            ProjectId = projectId,
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            CreatedAt = createdAt
        };

        var response = CustomizationAcceptedProductVersionFactory.ToCreateResponse(request, version);

        Assert.Equal(requestId, response.CustomizationRequestId);
        Assert.Equal(sourceVersionId, response.ProductVersionId);
        Assert.Equal(CustomizationStatus.DESIGN_REVIEWING, response.CustomizationStatus);
        Assert.Equal(productVersionId, response.ProductVersion.ProductVersionId);
        Assert.Equal(createdAt, response.CreatedAt);
    }
}
