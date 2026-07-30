#nullable enable

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CreateCustomizationProductVersionRequestDto
{
    public string? VersionName { get; set; }
    public string? VersionCode { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string? DimensionUnit { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public Guid? ModelFileId { get; set; }
    public IReadOnlyList<Guid> PreviewFileIds { get; set; } = [];
}
