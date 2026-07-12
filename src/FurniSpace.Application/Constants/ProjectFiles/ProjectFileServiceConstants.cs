using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectFiles;

internal static class ProjectFileServiceConstants
{
    internal const string ProjectFileIndexName = "project-files";
    internal const string AdminRole = "ADMIN";
    internal const string CustomerRole = "CUSTOMER";
    internal const string SalesRole = "SALES";
    internal const string DesignerRole = "DESIGNER";
    internal const string ProjectReferenceType = "PROJECT";
    internal const string InactiveOrMissingRoleMessage = "Authenticated account is not active or has no role.";
    internal const string FileNotFoundMessage = "File not found.";

    internal static readonly HashSet<string> SupportedReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectReferenceType,
        "PROJECT_SCHEDULE",
        "PROPOSAL",
        "QUOTATION",
        "ORDER",
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion
    };

    internal static readonly HashSet<string> CatalogReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion
    };

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
