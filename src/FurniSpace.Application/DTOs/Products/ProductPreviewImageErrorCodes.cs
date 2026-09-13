namespace FurniSpace.Application.DTOs.Products;

public static class ProductPreviewImageErrorCodes
{
    public const string MaxFilesExceeded = "MAX_FILES_EXCEEDED";
    public const string InvalidFileType = "INVALID_FILE_TYPE";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string InvalidReorderPayload = "INVALID_REORDER_PAYLOAD";
    public const string PreviewFileNotFound = "PREVIEW_FILE_NOT_FOUND";
    public const string UsePreviewFilesEndpoint = "USE_PREVIEW_FILES_ENDPOINT";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string FileNotBelongToProduct = "FILE_NOT_BELONG_TO_PRODUCT";
    public const string DuplicateFileId = "DUPLICATE_FILE_ID";
    public const string DuplicateDisplayOrder = "DUPLICATE_DISPLAY_ORDER";
    public const string Forbidden = "FORBIDDEN";
    public const string InvalidDisplayOrder = "INVALID_DISPLAY_ORDER";
}
