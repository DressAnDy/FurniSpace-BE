namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerFloorDocument
{
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public decimal? Rotation { get; set; }
    public decimal? Scale { get; set; }
}
