#nullable enable

using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class CustomizationAcceptedProductVersionFactory
{
    private const string DefaultDimensionUnit = "cm";
    private const int MaxVersionNameLength = 150;
    private const int MaxVersionCodeLength = 50;

    internal static ProductVersion CreateFromDesignerRequest(
        CreateCustomizationProductVersionRequestDto request,
        CustomizationRequest customizationRequest,
        ProductVersion sourceVersion,
        string projectCode,
        int sequence,
        string versionName,
        string? versionCode)
    {
        var now = DateTime.UtcNow;
        var originalUnitPrice = sourceVersion.EstimatedPrice ?? 0m;
        var additionalCost = customizationRequest.EstimatedAdditionalCost ?? 0m;

        return new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = sourceVersion.ProductId,
            ProjectId = customizationRequest.ProjectId,
            DimensionUnit = ResolveDimensionUnit(request.DimensionUnit, sourceVersion.DimensionUnit),
            VersionCode = string.IsNullOrWhiteSpace(versionCode)
                ? BuildVersionCode(projectCode, sequence)
                : versionCode.Trim(),
            VersionName = versionName,
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Material = Coalesce(request.Material, customizationRequest.RequestedMaterial, sourceVersion.Material),
            Color = Coalesce(request.Color, customizationRequest.RequestedColor, sourceVersion.Color),
            Width = Coalesce(request.Width, customizationRequest.RequestedWidth, sourceVersion.Width),
            Height = Coalesce(request.Height, customizationRequest.RequestedHeight, sourceVersion.Height),
            Depth = Coalesce(request.Depth, customizationRequest.RequestedDepth, sourceVersion.Depth),
            EstimatedPrice = request.EstimatedPrice ?? (originalUnitPrice + additionalCost),
            IsDefault = false,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static void LinkToCustomizationRequest(
        CustomizationRequest request,
        ProductVersion productVersion)
    {
        request.ApprovedProductVersionId = productVersion.ProductVersionId;
        request.UpdatedAt = DateTime.UtcNow;
    }

    internal static void MarkAccepted(CustomizationRequest request)
    {
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

    internal static CustomizationProductVersionDto ToProductVersionDto(ProductVersion version)
    {
        return new CustomizationProductVersionDto
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
            DimensionUnit = version.DimensionUnit,
            EstimatedPrice = version.EstimatedPrice,
            IsDefault = version.IsDefault,
            IsPublic = version.IsPublic,
            IsProjectSpecific = version.IsProjectSpecific,
            Status = version.Status
        };
    }

    internal static CreateCustomizationProductVersionResponseDto ToCreateResponse(
        CustomizationRequest request,
        ProductVersion version)
    {
        return new CreateCustomizationProductVersionResponseDto
        {
            CustomizationRequestId = request.CustomizationRequestId,
            ProjectId = request.ProjectId,
            ProductVersionId = request.ProductVersionId,
            ProductVersion = ToProductVersionDto(version),
            CustomizationStatus = request.Status,
            CreatedAt = version.CreatedAt ?? DateTime.UtcNow
        };
    }

    internal static string? ValidateVersionName(string? versionName)
    {
        if (string.IsNullOrWhiteSpace(versionName))
        {
            return "Version name is required.";
        }

        return versionName.Trim().Length > MaxVersionNameLength
            ? $"Version name must be at most {MaxVersionNameLength} characters."
            : null;
    }

    internal static string? ValidateVersionCode(string? versionCode)
    {
        if (string.IsNullOrWhiteSpace(versionCode))
        {
            return null;
        }

        return versionCode.Trim().Length > MaxVersionCodeLength
            ? $"Version code must be at most {MaxVersionCodeLength} characters."
            : null;
    }

    internal static bool IsValidDimensionUnit(string? dimensionUnit)
    {
        if (string.IsNullOrWhiteSpace(dimensionUnit))
        {
            return false;
        }

        var normalized = dimensionUnit.Trim().ToLowerInvariant();
        return normalized is "cm" or "m" or "mm";
    }

    internal static string BuildVersionCode(string projectCode, int sequence)
    {
        return $"PV-{projectCode}-CUST-{sequence:D3}";
    }

    private static string ResolveDimensionUnit(string? requested, string? source)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested.Trim().ToLowerInvariant();
        }

        return string.IsNullOrWhiteSpace(source)
            ? DefaultDimensionUnit
            : source.Trim();
    }

    private static string? Coalesce(string? first, string? second, string? third)
    {
        return Coalesce(first, Coalesce(second, third));
    }

    private static string? Coalesce(string? requested, string? original)
    {
        return string.IsNullOrWhiteSpace(requested) ? original : requested.Trim();
    }

    private static decimal? Coalesce(decimal? first, decimal? second, decimal? third)
    {
        return first ?? second ?? third;
    }
}
