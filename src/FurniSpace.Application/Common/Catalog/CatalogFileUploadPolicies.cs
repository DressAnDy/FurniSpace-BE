using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Common.Catalog;

public static class CatalogFileUploadPolicies
{
    public static readonly HashSet<FileType> ProductAllowedFileTypes =
    [
        FileType.PRODUCT_PREVIEW
    ];

    public static readonly HashSet<FileType> ProductVersionAllowedFileTypes =
    [
        FileType.PRODUCT_PREVIEW,
        FileType.MODEL_3D,
        FileType.TEXTURE
    ];

    public static CatalogReferenceFileUploadOptions ForProduct(
        Guid productId,
        FirebaseStorageSettings settings)
    {
        return new CatalogReferenceFileUploadOptions
        {
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = productId,
            AllowedFileTypes = ProductAllowedFileTypes,
            StoragePrefixDefault = "products",
            StoragePrefixConfigured = settings.ProductFilesPrefix,
            SuccessMessage = "Product file uploaded successfully."
        };
    }

    public static CatalogReferenceFileUploadOptions ForProductVersion(
        Guid productVersionId,
        FirebaseStorageSettings settings)
    {
        return new CatalogReferenceFileUploadOptions
        {
            ReferenceType = CatalogFileReferenceTypes.ProductVersion,
            ReferenceId = productVersionId,
            AllowedFileTypes = ProductVersionAllowedFileTypes,
            StoragePrefixDefault = "product-versions",
            StoragePrefixConfigured = settings.ProductVersionFilesPrefix,
            SuccessMessage = "Product version file uploaded successfully."
        };
    }
}
