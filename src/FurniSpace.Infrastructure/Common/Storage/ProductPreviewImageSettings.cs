namespace FurniSpace.Infrastructure.Common.Storage;

/// <summary>
/// Catalog preview image upload rules. Defaults live here — not in appsettings.
/// Override only via environment-specific deployment config when truly needed.
/// </summary>
public sealed class ProductPreviewImageSettings
{
    public const string SectionName = "ProductPreviewImages";

    public const int DefaultMaxCount = 5;

    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;

    public static readonly string[] DefaultAllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
    ];

    public static readonly string[] DefaultAllowedMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/svg+xml"
    ];

    public int MaxCount { get; set; } = DefaultMaxCount;

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    public string[] AllowedExtensions { get; set; } = DefaultAllowedExtensions;

    public string[] AllowedMimeTypes { get; set; } = DefaultAllowedMimeTypes;

    public static ProductPreviewImageSettings CreateDefault() => new();
}
