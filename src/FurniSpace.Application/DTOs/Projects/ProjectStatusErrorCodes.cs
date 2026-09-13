namespace FurniSpace.Application.DTOs.Projects;

public static class ProjectStatusErrorCodes
{
    public const string InvalidProjectStatusTransition = "INVALID_PROJECT_STATUS_TRANSITION";
    public const string DesignerNotAssigned = "DESIGNER_NOT_ASSIGNED";
    public const string MeasurementNotCompleted = "MEASUREMENT_NOT_COMPLETED";
    public const string MeasurementFileRequired = "MEASUREMENT_FILE_REQUIRED";
    public const string NoteRequired = "NOTE_REQUIRED";
    public const string FinalProposalRequired = "FINAL_PROPOSAL_REQUIRED";
    public const string InvalidProjectStatus = "INVALID_PROJECT_STATUS";
    public const string Forbidden = "FORBIDDEN";
}
