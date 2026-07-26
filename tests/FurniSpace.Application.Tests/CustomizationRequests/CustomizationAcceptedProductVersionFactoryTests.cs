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
    public void Create_UsesRequestedValuesAndBuildsProjectSpecificVersion()
    {
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = projectId,
            RequestedMaterial = " Dark oak ",
            RequestedColor = "Brown",
            RequestedWidth = 55m,
            EstimatedAdditionalCost = 500000m
        };
        var originalVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            Material = "Oak",
            Color = "Natural",
            Width = 50m,
            Height = 80m,
            Depth = 50m,
            DimensionUnit = "cm",
            EstimatedPrice = 1000000m
        };
        var proposalItem = new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ItemName = "Cafe Chair",
            Quantity = 2,
            UnitPriceSnapshot = 1200000m
        };

        var approvedVersion = CustomizationAcceptedProductVersionFactory.Create(
            request,
            originalVersion,
            proposalItem,
            "PRJ-000001",
            2);

        Assert.Equal(ProductVersionType.PROJECT_SPECIFIC, approvedVersion.VersionType);
        Assert.Equal("PV-PRJ-000001-CUST-002", approvedVersion.VersionCode);
        Assert.Equal("Cafe Chair - Project PRJ-000001 Custom", approvedVersion.VersionName);
        Assert.Equal("Dark oak", approvedVersion.Material);
        Assert.Equal("Brown", approvedVersion.Color);
        Assert.Equal(55m, approvedVersion.Width);
        Assert.Equal(1700000m, approvedVersion.EstimatedPrice);
        Assert.False(approvedVersion.IsPublic);
        Assert.True(approvedVersion.IsProjectSpecific);
        Assert.Equal(projectId, approvedVersion.ProjectId);
    }

    [Fact]
    public void Create_FallsBackToOriginalValuesWhenRequestedFieldsAreNull()
    {
        var request = new CustomizationRequest
        {
            ProjectId = Guid.NewGuid(),
            EstimatedAdditionalCost = 0m
        };
        var originalVersion = new ProductVersion
        {
            ProductId = Guid.NewGuid(),
            Material = "Oak",
            Color = "Natural",
            Width = 50m,
            Height = 80m,
            Depth = 45m
        };
        var proposalItem = new ProposalItem
        {
            ItemName = "Chair",
            UnitPriceSnapshot = 1000000m
        };

        var approvedVersion = CustomizationAcceptedProductVersionFactory.Create(
            request,
            originalVersion,
            proposalItem,
            "PRJ-000002",
            1);

        Assert.Equal("Oak", approvedVersion.Material);
        Assert.Equal("Natural", approvedVersion.Color);
        Assert.Equal(50m, approvedVersion.Width);
        Assert.Equal(80m, approvedVersion.Height);
        Assert.Equal(45m, approvedVersion.Depth);
        Assert.Equal(1000000m, approvedVersion.EstimatedPrice);
    }

    [Fact]
    public void ApplyAcceptedChanges_UpdatesProposalItemAndRequest()
    {
        var now = DateTime.UtcNow;
        var request = new CustomizationRequest { Status = CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL };
        var proposalItem = new ProposalItem
        {
            Quantity = 4,
            UnitPriceSnapshot = 1000000m,
            TotalPriceSnapshot = 4000000m
        };
        var approvedVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            Material = "Walnut",
            Color = "Dark",
            Width = 60m,
            Height = 90m,
            Depth = 40m,
            EstimatedPrice = 1500000m,
            UpdatedAt = now
        };

        CustomizationAcceptedProductVersionFactory.ApplyAcceptedChanges(request, proposalItem, approvedVersion);

        Assert.Equal(approvedVersion.ProductVersionId, proposalItem.ApprovedProductVersionId);
        Assert.Equal(approvedVersion.ProductVersionId, request.ApprovedProductVersionId);
        Assert.Equal(CustomizationStatus.ACCEPTED, request.Status);
        Assert.Equal(1500000m, proposalItem.UnitPriceSnapshot);
        Assert.Equal(6000000m, proposalItem.TotalPriceSnapshot);
        Assert.True(proposalItem.IsCustomized);
        Assert.NotNull(request.CustomerAcceptedAt);
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
}
