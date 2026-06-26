namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerStyleDocument
{
    public string? Color { get; set; }
    public string? MaterialCode { get; set; }
    public Guid? TextureFileId { get; set; }
    public decimal? TextureRotation { get; set; }
    public decimal? TextureScale { get; set; }
}
