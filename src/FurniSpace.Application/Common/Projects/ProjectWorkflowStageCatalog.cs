#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectWorkflowStageCatalog
{
    public const string StateNotStarted = "NOT_STARTED";
    public const string StateActive = "ACTIVE";
    public const string StateCompleted = "COMPLETED";
    public const string StateBlocked = "BLOCKED";

    public const string StageIntake = "INTAKE";
    public const string StageDesignerAssignment = "DESIGNER_ASSIGNMENT";
    public const string StageDesignReview = "DESIGN_REVIEW";
    public const string StageQuotationOrder = "QUOTATION_ORDER";
    public const string StageProduction = "PRODUCTION";
    public const string StageDelivery = "DELIVERY";

    public static readonly IReadOnlyList<StageDefinition> Stages =
    [
        new(
            StageIntake,
            "Intake",
            [
                ProjectStatus.SUBMITTED,
                ProjectStatus.IN_CONSULTATION,
                ProjectStatus.NEED_BASIC_INFORMATION
            ],
            ProjectStatus.IN_CONSULTATION),
        new(
            StageDesignerAssignment,
            "Designer assignment",
            [
                ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
                ProjectStatus.MEASUREMENT_REQUIRED,
                ProjectStatus.SPACE_VERIFIED
            ],
            ProjectStatus.SPACE_VERIFIED),
        new(
            StageDesignReview,
            "Design review",
            [
                ProjectStatus.PROPOSAL_CONSULTING,
                ProjectStatus.PROPOSAL_SELECTED
            ],
            ProjectStatus.PROPOSAL_SELECTED),
        new(
            StageQuotationOrder,
            "Quotation & order",
            [
                ProjectStatus.QUOTATION_SENT,
                ProjectStatus.QUOTATION_REVISION_REQUESTED,
                ProjectStatus.ORDER_CONFIRMED
            ],
            ProjectStatus.ORDER_CONFIRMED),
        new(
            StageProduction,
            "Production",
            [
                ProjectStatus.IN_PRODUCTION,
                ProjectStatus.PRODUCTION_BLOCKED,
                ProjectStatus.READY_FOR_DELIVERY
            ],
            ProjectStatus.READY_FOR_DELIVERY),
        new(
            StageDelivery,
            "Delivery",
            [
                ProjectStatus.DELIVERING,
                ProjectStatus.DELIVERED,
                ProjectStatus.COMPLETED
            ],
            ProjectStatus.COMPLETED)
    ];

    public static int? ResolveStageIndex(ProjectStatus? status)
    {
        if (!status.HasValue || status == ProjectStatus.REJECTED)
        {
            return null;
        }

        for (var i = 0; i < Stages.Count; i++)
        {
            if (Stages[i].Statuses.Contains(status.Value))
            {
                return i;
            }
        }

        return null;
    }

    public sealed record StageDefinition(
        string Key,
        string Label,
        IReadOnlyList<ProjectStatus> Statuses,
        ProjectStatus CompletedDisplayStatus);
}
