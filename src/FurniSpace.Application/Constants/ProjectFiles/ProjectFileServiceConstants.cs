using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.ProjectFiles;

internal static class ProjectFileServiceConstants
{
    internal const string ProjectFileIndexName = "project-files";
    internal const string ProjectReferenceType = "PROJECT";
    internal const string ProjectAreaReferenceType = "PROJECT_AREA";
    internal const string InactiveOrMissingRoleMessage = "Authenticated account is not active or has no role.";
    internal const string FileNotFoundMessage = "File not found.";

    internal static readonly HashSet<string> SupportedReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectReferenceType,
        ProjectAreaReferenceType,
        "PROJECT_SCHEDULE",
        "PROPOSAL",
        "QUOTATION",
        "ORDER",
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion,
        CatalogFileReferenceTypes.LayoutAsset
    };

    internal static readonly HashSet<string> CatalogReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion,
        CatalogFileReferenceTypes.LayoutAsset
    };

    internal static readonly IReadOnlyDictionary<ProjectStatus, int> ProjectStatusRanks = ProjectStatusRankings.Values;
}
