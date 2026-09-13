namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class SubmitCustomizationRequestDto
{
    public string RequestTitle { get; set; } = string.Empty;
    public string? RequestDescription { get; set; }
    public decimal? RequestedWidth { get; set; }
    public decimal? RequestedHeight { get; set; }
    public decimal? RequestedDepth { get; set; }
    public string? RequestedMaterial { get; set; }
    public string? RequestedColor { get; set; }
    public string? RequestedChangeNote { get; set; }
}
