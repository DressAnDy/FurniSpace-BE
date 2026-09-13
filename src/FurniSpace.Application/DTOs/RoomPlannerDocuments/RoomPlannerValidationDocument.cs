namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerValidationDocument
{
    public string Status { get; set; } = "NOT_VALIDATED";
    public List<RoomPlannerValidationIssueDocument> Warnings { get; set; } = [];
    public List<RoomPlannerValidationIssueDocument> Errors { get; set; } = [];
    public DateTime? LastValidatedAt { get; set; }
}
