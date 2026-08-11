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
    public void CreateRequestVersion_CreatesDraftVersionWithPendingFeasibility()
    {
        var request = new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            Status = CustomizationStatus.SUBMITTED
        };
        var productVersion = new ProductVersion { ProductVersionId = Guid.NewGuid() };
        var dto = new CreateCustomizationRequestVersionDto
        {
            VersionTitle = "Walnut option",
            DesignerNote = "Reinforced frame"
        };

        var version = CustomizationAcceptedProductVersionFactory.CreateRequestVersion(
            request,
            productVersion,
            1,
            Guid.NewGuid(),
            dto);

        Assert.Equal(request.CustomizationRequestId, version.CustomizationRequestId);
        Assert.Equal(productVersion.ProductVersionId, version.ProductVersionId);
        Assert.Equal(1, version.VersionNo);
        Assert.Equal("Walnut option", version.VersionTitle);
        Assert.Equal(CustomizationVersionStatus.DRAFT, version.Status);
        Assert.Equal(ProductionFeasibilityStatus.PENDING, version.FeasibilityStatus);
    }

    [Fact]
    public void MarkRequestAccepted_SetsAcceptedRequestVersionAndStatuses()
    {
        var request = new CustomizationRequest { Status = CustomizationStatus.REVIEWING };
        var version = new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            Status = CustomizationVersionStatus.REVIEWING,
            FeasibilityStatus = ProductionFeasibilityStatus.FEASIBLE
        };
        var acceptedAt = DateTime.UtcNow;

        CustomizationAcceptedProductVersionFactory.MarkRequestAccepted(request, version, acceptedAt);

        Assert.Equal(CustomizationStatus.ACCEPTED, request.Status);
        Assert.Equal(version.CustomizationRequestVersionId, request.AcceptedRequestVersionId);
        Assert.Equal(CustomizationVersionStatus.ACCEPTED, version.Status);
        Assert.Equal(acceptedAt, version.AcceptedAt);
    }

    [Fact]
    public void CreateFromDesignerRequest_PrefersRequestValuesThenCustomizationThenSource()
    {
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var requestDto = new CreateCustomizationRequestVersionDto
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
            RequestedHeight = 90m
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
    }

    [Fact]
    public void CalculateAcceptedFinalPrice_AddsSourcePriceAndAdditionalCost()
    {
        var finalPrice = CustomizationAcceptedProductVersionFactory.CalculateAcceptedFinalPrice(
            1_000_000m,
            1_500_000m);

        Assert.Equal(2_500_000m, finalPrice);
    }

    [Fact]
    public void ApplyAcceptedFinalPrice_UpdatesEstimatedPrice()
    {
        var version = new ProductVersion { EstimatedPrice = 1_000_000m };
        var updatedAt = DateTime.UtcNow;

        CustomizationAcceptedProductVersionFactory.ApplyAcceptedFinalPrice(version, 2_500_000m, updatedAt);

        Assert.Equal(2_500_000m, version.EstimatedPrice);
        Assert.Equal(updatedAt, version.UpdatedAt);
    }

    [Fact]
    public void ToCreateVersionResponse_MapsVersionAndProductVersion()
    {
        var requestId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var request = new CustomizationRequest
        {
            CustomizationRequestId = requestId,
            ProjectId = Guid.NewGuid(),
            SourceProductVersionId = Guid.NewGuid(),
            Status = CustomizationStatus.REVIEWING
        };
        var requestVersion = new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = versionId,
            CustomizationRequestId = requestId,
            VersionNo = 1,
            CreatedByDesignerId = Guid.NewGuid(),
            Status = CustomizationVersionStatus.DRAFT,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var productVersion = new ProductVersion
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-PRJ-000001-CUST-001",
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            CreatedAt = DateTime.UtcNow
        };

        var response = CustomizationAcceptedProductVersionFactory.ToCreateVersionResponse(
            request,
            requestVersion,
            productVersion);

        Assert.Equal(requestId, response.CustomizationRequestId);
        Assert.Equal(versionId, response.CustomizationRequestVersionId);
        Assert.Equal(productVersionId, response.Version.ProductVersion.ProductVersionId);
    }

    [Fact]
    public void ApplyDraftMetadata_UpdatesVersionTitleAndDesignerNote()
    {
        var version = new CustomizationRequestVersion
        {
            VersionTitle = "Old title",
            DesignerNote = "Old note",
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var dto = new UpdateCustomizationRequestVersionDto
        {
            VersionTitle = "  New title  ",
            DesignerNote = "Updated note"
        };

        CustomizationAcceptedProductVersionFactory.ApplyDraftMetadata(version, dto);

        Assert.Equal("New title", version.VersionTitle);
        Assert.Equal("Updated note", version.DesignerNote);
    }

    [Fact]
    public void CreateFromDesignerRequest_UpdateOverload_UpdatesExistingProductVersion()
    {
        var customizationRequest = new CustomizationRequest
        {
            ProjectId = Guid.NewGuid(),
            RequestedMaterial = "Oak",
            RequestedColor = "Brown"
        };
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            Material = "Pine",
            Color = "Natural",
            Width = 60m,
            DimensionUnit = "cm",
            EstimatedPrice = 1000000m
        };
        var existingVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            VersionCode = "PV-001",
            VersionName = "Old Name",
            Material = "Pine",
            EstimatedPrice = 1000000m
        };
        var updateDto = new UpdateCustomizationRequestVersionDto
        {
            Material = "Walnut",
            Width = 70m,
            EstimatedPrice = 1500000m,
            DimensionUnit = "mm"
        };

        var result = CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
            updateDto,
            customizationRequest,
            sourceVersion,
            existingVersion,
            "Updated Name",
            "PV-002");

        Assert.Same(existingVersion, result);
        Assert.Equal("Updated Name", result.VersionName);
        Assert.Equal("PV-002", result.VersionCode);
        Assert.Equal("Walnut", result.Material);
        Assert.Equal(70m, result.Width);
        Assert.Equal("mm", result.DimensionUnit);
        Assert.Equal(1500000m, result.EstimatedPrice);
    }

    [Fact]
    public void ValidateVersionName_WhenTooLong_ReturnsError()
    {
        var error = CustomizationAcceptedProductVersionFactory.ValidateVersionName(new string('A', 151));

        Assert.NotNull(error);
        Assert.Contains("at most", error);
    }

    [Fact]
    public void ValidateVersionCode_WhenTooLong_ReturnsError()
    {
        var error = CustomizationAcceptedProductVersionFactory.ValidateVersionCode(new string('C', 51));

        Assert.NotNull(error);
        Assert.Contains("at most", error);
    }
}
