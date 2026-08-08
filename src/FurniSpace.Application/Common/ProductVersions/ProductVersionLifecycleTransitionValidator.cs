using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.ProductVersions;

internal static class ProductVersionLifecycleTransitionValidator
{
    internal static bool CanActivate(ProductStatus? currentStatus)
    {
        return currentStatus == ProductStatus.INACTIVE;
    }

    internal static bool CanDeactivate(ProductStatus? currentStatus)
    {
        return currentStatus == ProductStatus.ACTIVE;
    }

    internal static bool CanArchive(ProductStatus? currentStatus)
    {
        return currentStatus is ProductStatus.ACTIVE or ProductStatus.INACTIVE;
    }

    internal static bool CanRestore(ProductStatus? currentStatus)
    {
        return currentStatus == ProductStatus.ARCHIVED;
    }

    internal static bool IsActive(ProductStatus? status)
    {
        return status is null or ProductStatus.ACTIVE;
    }
}
