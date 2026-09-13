namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerValidationIssueDocument
{
    public string Code { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string? ObjectId { get; set; }
    public Guid? LayoutAssetId { get; set; }
    public string Message { get; set; } = string.Empty;
}
