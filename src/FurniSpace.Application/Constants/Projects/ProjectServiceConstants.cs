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
    internal static readonly Dictionary<ProjectStatus, int> ProjectStatusRanks = new()
    {
        [ProjectStatus.SUBMITTED] = 10,
        [ProjectStatus.IN_CONSULTATION] = 20,
        [ProjectStatus.NEED_BASIC_INFORMATION] = 30,
        [ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT] = 40,
        [ProjectStatus.MEASUREMENT_REQUIRED] = 50,
        [ProjectStatus.SPACE_VERIFIED] = 60,
        [ProjectStatus.PROPOSAL_CONSULTING] = 80,
        [ProjectStatus.PROPOSAL_SELECTED] = 100,
        [ProjectStatus.QUOTATION_SENT] = 110,
        [ProjectStatus.QUOTATION_REVISION_REQUESTED] = 120,
        [ProjectStatus.ORDER_CONFIRMED] = 130,
        [ProjectStatus.IN_PRODUCTION] = 140,
        [ProjectStatus.PRODUCTION_BLOCKED] = 150,
        [ProjectStatus.READY_FOR_DELIVERY] = 160,
        [ProjectStatus.DELIVERING] = 170,
        [ProjectStatus.DELIVERED] = 180,
        [ProjectStatus.COMPLETED] = 190,
        [ProjectStatus.REJECTED] = 200
    };
}
