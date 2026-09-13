namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerEditorStateDocument
{
    public string? ActiveTool { get; set; }
    public string? SelectedObjectId { get; set; }
    public string? ViewMode { get; set; }
    public bool? GridEnabled { get; set; }
    public bool? SnapEnabled { get; set; }
    public Dictionary<string, object?> SnapSettings { get; set; } = [];
}
