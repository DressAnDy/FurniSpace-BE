using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class ApprovedProductVersionSummaryMapper
{
    internal static ApprovedProductVersionSummaryDto ToDto(ProductVersion version)
    {
        return CustomizationAcceptedProductVersionFactory.ToSummaryDto(version);
    }

    internal static ApprovedProductVersionSummaryDto ToDto(
        ProductVersionDetailReadModel version,
        Guid? projectId = null)
    {
        return new ApprovedProductVersionSummaryDto
        {
            ProductVersionId = version.ProductVersionId,
            ProductId = version.ProductId,
            ProjectId = projectId,
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
}
