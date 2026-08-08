using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Catalog;

internal static class ProjectCatalogEligibility
{
    internal static bool IsEligibleProductStatus(ProductStatus? status)
    {
        return status == ProductStatus.ACTIVE;
    }

    internal static bool IsEligibleVersionStatus(ProductStatus? status)
    {
        return status == ProductStatus.ACTIVE;
    }

    internal static bool IsEligibleVersion(
        ProductStatus? productStatus,
        ProductStatus? versionStatus,
        bool? isPublic,
        bool? isProjectSpecific,
        Guid? versionProjectId,
        Guid requestedProjectId)
    {
        if (!IsEligibleProductStatus(productStatus) || !IsEligibleVersionStatus(versionStatus))
        {
            return false;
        }

        if (isPublic == true)
        {
            return true;
        }

        return isProjectSpecific == true &&
               versionProjectId.HasValue &&
               versionProjectId.Value == requestedProjectId;
    }
}
