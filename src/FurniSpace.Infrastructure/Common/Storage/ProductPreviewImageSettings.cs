namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class ProductPreviewImageSettings
{
    public const string SectionName = "ProductPreviewImages";

    public int MaxCount { get; set; } = 5;

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
    ];

    public string[] AllowedMimeTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml"
    ];
}
