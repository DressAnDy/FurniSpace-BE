namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class FileUploadSettings
{
    public const string SectionName = "FileUpload";

    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg",
        ".mp4", ".mov", ".webm",
        ".glb", ".gltf", ".obj", ".fbx", ".stl", ".usdz",
        ".pdf", ".dwg", ".dxf", ".ifc", ".skp",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".zip", ".rar", ".7z"
    ];

    public string[] AllowedMimeTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml",
        "video/mp4", "video/quicktime", "video/webm",
        "model/gltf-binary", "model/gltf+json", "model/obj", "model/stl",
        "application/pdf", "application/acad", "application/x-acad", "application/autocad",
        "application/dwg", "application/x-dwg", "image/vnd.dwg",
        "application/dxf", "image/vnd.dxf",
        "application/octet-stream",
        "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain", "text/csv",
        "application/zip", "application/x-rar-compressed", "application/x-7z-compressed"
    ];
}
