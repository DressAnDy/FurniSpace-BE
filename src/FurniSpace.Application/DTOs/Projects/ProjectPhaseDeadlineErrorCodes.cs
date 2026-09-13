namespace FurniSpace.Application.DTOs.Projects;

public static class ProjectPhaseDeadlineErrorCodes
{
    public const string ProposalDeadlineRequired = "PROPOSAL_DEADLINE_REQUIRED";
    public const string ProposalDeadlineInvalid = "PROPOSAL_DEADLINE_INVALID";
    public const string ProductionDeadlineRequired = "PRODUCTION_DEADLINE_REQUIRED";
    public const string ProductionDeadlineInvalid = "PRODUCTION_DEADLINE_INVALID";
    public const string PhaseDeadlineUpsertDeprecated = "PHASE_DEADLINE_UPSERT_DEPRECATED";
    public const string InvalidProjectStatus = "INVALID_PROJECT_STATUS";
    public const string OrderRequired = "ORDER_REQUIRED";
}
