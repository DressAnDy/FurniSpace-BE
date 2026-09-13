namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerLayerDocument
{
    public string LayerId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}
