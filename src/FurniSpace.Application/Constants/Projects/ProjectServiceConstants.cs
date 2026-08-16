using FurniSpace.Domain.Enums;
using FurniSpace.Application.Constants.Common;

namespace FurniSpace.Application.Constants.Projects;

internal static class ProjectServiceConstants
{
    internal const string ProjectIndexName = "projects";
    internal const int MaxNoteLength = 1000;
    internal const int MaxRejectionReasonLength = 1000;
    internal const string ProjectReferenceType = "PROJECT";
    internal const string ProjectNameNotificationKey = "ProjectName";
    internal const string AuthenticatedAccountIdRequiredMessage = "Authenticated account id is required.";
    internal const string ProjectIdRequiredMessage = "Project id is required.";
    internal const string ProjectNotFoundMessage = "Project not found.";
    internal const int MaxProjectsByUserPageSize = 100;

    internal static readonly string[] ProjectSubmittedReceiverRoles = [ApplicationRoles.Sales, ApplicationRoles.Admin];
    internal static readonly IReadOnlyDictionary<ProjectStatus, int> ProjectStatusRanks = ProjectStatusRankings.Values;

    internal static readonly ProjectStatus[] ReopenEligibleProjectStatuses =
    [
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.ORDER_CONFIRMED
    ];

    internal static readonly OrderStatus[] ReopenEligibleOrderStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING
    ];
}
