using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class CustomizationAcceptedProductVersionFactory
{
    internal static ProductVersion Create(
        CustomizationRequest request,
        ProductVersion originalVersion,
        ProposalItem proposalItem,
        string projectCode,
        int sequence)
    {
        var material = Coalesce(request.RequestedMaterial, originalVersion.Material);
        var color = Coalesce(request.RequestedColor, originalVersion.Color);
        var width = Coalesce(request.RequestedWidth, originalVersion.Width);
        var height = Coalesce(request.RequestedHeight, originalVersion.Height);
        var depth = Coalesce(request.RequestedDepth, originalVersion.Depth);
        var originalUnitPrice = proposalItem.UnitPriceSnapshot ?? originalVersion.EstimatedPrice ?? 0m;
        var additionalCost = request.EstimatedAdditionalCost ?? 0m;
        var now = DateTime.UtcNow;

        return new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = originalVersion.ProductId,
            ProjectId = request.ProjectId,
            DimensionUnit = originalVersion.DimensionUnit ?? "cm",
            VersionCode = BuildVersionCode(projectCode, sequence),
            VersionName = $"{proposalItem.ItemName} - Project {projectCode} Custom",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Material = material,
            Color = color,
            Width = width,
            Height = height,
            Depth = depth,
            EstimatedPrice = originalUnitPrice + additionalCost,
            IsDefault = false,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static void ApplyAcceptedChanges(
        CustomizationRequest request,
        ProposalItem proposalItem,
        ProductVersion approvedVersion)
    {
        var customizedUnitPrice = approvedVersion.EstimatedPrice ?? proposalItem.UnitPriceSnapshot ?? 0m;
        var quantity = proposalItem.Quantity ?? 0;

        proposalItem.ApprovedProductVersionId = approvedVersion.ProductVersionId;
        proposalItem.Material = approvedVersion.Material;
        proposalItem.Color = approvedVersion.Color;
        proposalItem.Width = approvedVersion.Width;
        proposalItem.Height = approvedVersion.Height;
        proposalItem.Depth = approvedVersion.Depth;
        proposalItem.UnitPriceSnapshot = customizedUnitPrice;
        proposalItem.TotalPriceSnapshot = customizedUnitPrice * quantity;
        proposalItem.IsCustomized = true;
        proposalItem.UpdatedAt = DateTime.UtcNow;

        request.ApprovedProductVersionId = approvedVersion.ProductVersionId;
        request.Status = CustomizationStatus.ACCEPTED;
        request.CustomerAcceptedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
    }

    internal static ApprovedProductVersionSummaryDto ToSummaryDto(ProductVersion version)
    {
        return new ApprovedProductVersionSummaryDto
        {
            ProductVersionId = version.ProductVersionId,
            ProductId = version.ProductId,
            ProjectId = version.ProjectId,
            VersionCode = version.VersionCode,
            VersionName = version.VersionName,
            VersionType = version.VersionType,
            Material = version.Material,
            Color = version.Color,
            Width = version.Width,
            Height = version.Height,
            Depth = version.Depth,
            EstimatedPrice = version.EstimatedPrice,
            IsDefault = version.IsDefault,
            IsPublic = version.IsPublic,
            IsProjectSpecific = version.IsProjectSpecific,
            Status = version.Status
        };
    }

    private static string BuildVersionCode(string projectCode, int sequence)
    {
        return $"PV-{projectCode}-CUST-{sequence:D3}";
    }

    private static string? Coalesce(string? requested, string? original)
    {
        return string.IsNullOrWhiteSpace(requested) ? original : requested.Trim();
    }

    private static decimal? Coalesce(decimal? requested, decimal? original)
    {
        return requested ?? original;
    }
}
