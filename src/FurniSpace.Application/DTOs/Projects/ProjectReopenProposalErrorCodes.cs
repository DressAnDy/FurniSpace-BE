namespace FurniSpace.Application.DTOs.Projects;

public static class ProjectReopenProposalErrorCodes
{
    public const string ReopenNotAllowed = "PROJECT_REOPEN_NOT_ALLOWED";
    public const string DepositAlreadyPaid = "PROJECT_DEPOSIT_ALREADY_PAID";
    public const string ProductionAlreadyCreated = "PROJECT_PRODUCTION_ALREADY_CREATED";
    public const string NoAcceptedOrder = "PROJECT_NO_ACCEPTED_ORDER";
    public const string ActiveDepositCannotBeCancelled = "ACTIVE_DEPOSIT_CANNOT_BE_CANCELLED";
}
