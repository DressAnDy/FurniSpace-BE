using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Products;

internal static class ProductLifecycleTransitionValidator
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
}
