namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class FirebaseStorageSettings
{
    public const string SectionName = "FirebaseStorage";

    public string Bucket { get; set; } = string.Empty;
    public string? CredentialsPath { get; set; }
    public string ProjectFilesPrefix { get; set; } = "projects";
    public string ProductFilesPrefix { get; set; } = "products";
    public string ProductVersionFilesPrefix { get; set; } = "product-versions";
    public string LayoutAssetFilesPrefix { get; set; } = "layout-assets";
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
}
