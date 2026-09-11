using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.LayoutAssets;

internal static class LayoutAssetServiceConstants
{
    internal const string CreatedMessage = "Layout asset created successfully.";
    internal const string UpdatedMessage = "Layout asset updated successfully.";
    internal const string StatusUpdatedMessage = "Layout asset status updated successfully.";
    internal const string RetrievedMessage = "Layout assets retrieved successfully.";
    internal const string DetailRetrievedMessage = "Layout asset retrieved successfully.";
    internal const string CatalogRetrievedMessage = "Room planner layout assets retrieved successfully.";
    internal const string FilesRetrievedMessage = "Layout asset files retrieved successfully.";
    internal const string FileUploadedMessage = "Layout asset file uploaded successfully.";
    internal const string FileDeletedMessage = "Layout asset file deleted successfully.";
    internal const string PrimaryFileUpdatedMessage = "Layout asset primary file updated successfully.";

    internal static readonly HashSet<FileType> AllowedLayoutAssetFileTypes =
    [
        FileType.MODEL_3D,
        FileType.TEXTURE,
        FileType.PREVIEW,
        FileType.OTHER
    ];

    internal static readonly HashSet<FileType> PrimaryEligibleFileTypes =
    [
        FileType.MODEL_3D,
        FileType.TEXTURE,
        FileType.PREVIEW,
        FileType.PRODUCT_PREVIEW
    ];
}
