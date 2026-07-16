using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProductVersions;

internal static class ProductVersionServiceConstants
{
    internal const string ProductVersionIdRequiredMessage = "Product version id is required.";
    internal const string ProductVersionNotFoundMessage = "Product version not found.";
    internal const string PreviewFileNotFoundMessage = "Product version preview image not found.";

    internal static readonly HashSet<FileType> AllowedProductVersionFileTypes =
    [
        FileType.PRODUCT_PREVIEW,
        FileType.MODEL_3D,
        FileType.TEXTURE,
        FileType.OTHER
    ];
}
