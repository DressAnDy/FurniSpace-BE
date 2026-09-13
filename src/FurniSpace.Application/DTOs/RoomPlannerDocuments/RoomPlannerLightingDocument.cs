namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerLightingDocument
{
    public string Preset { get; set; } = "DEFAULT";
    public string? Environment { get; set; } = "default";
    public decimal? AmbientIntensity { get; set; }
    public decimal? DirectionalIntensity { get; set; }
    public List<Dictionary<string, object?>> CustomLights { get; set; } = [];
}
