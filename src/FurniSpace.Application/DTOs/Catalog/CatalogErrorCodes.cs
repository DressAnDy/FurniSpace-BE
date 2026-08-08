namespace FurniSpace.Application.DTOs.Catalog;

public static class CatalogErrorCodes
{
    public const string CatalogAdminAccessDenied = "CATALOG_ADMIN_ACCESS_DENIED";
    public const string CatalogFilterInvalid = "CATALOG_FILTER_INVALID";
    public const string CatalogSortInvalid = "CATALOG_SORT_INVALID";
    public const string CategoryNotFound = "CATEGORY_NOT_FOUND";
    public const string BusinessTypeNotFound = "BUSINESS_TYPE_NOT_FOUND";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string ProductAccessDenied = "PRODUCT_ACCESS_DENIED";
    public const string ProductInvalidStatusTransition = "PRODUCT_INVALID_STATUS_TRANSITION";
    public const string ProductAlreadyActive = "PRODUCT_ALREADY_ACTIVE";
    public const string ProductAlreadyInactive = "PRODUCT_ALREADY_INACTIVE";
    public const string ProductAlreadyArchived = "PRODUCT_ALREADY_ARCHIVED";
    public const string ProductRestoreNotAllowed = "PRODUCT_RESTORE_NOT_ALLOWED";
    public const string ProductVersionNotFound = "PRODUCT_VERSION_NOT_FOUND";
    public const string ProductVersionAccessDenied = "PRODUCT_VERSION_ACCESS_DENIED";
    public const string ProductVersionInvalidStatusTransition = "PRODUCT_VERSION_INVALID_STATUS_TRANSITION";
    public const string ProductVersionDefaultInactive = "PRODUCT_VERSION_DEFAULT_INACTIVE";
    public const string ProductVersionDefaultArchived = "PRODUCT_VERSION_DEFAULT_ARCHIVED";
    public const string ProductVersionTaxRateInvalid = "PRODUCT_VERSION_TAX_RATE_INVALID";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string ProjectAccessDenied = "PROJECT_ACCESS_DENIED";
    public const string DesignerNotAssigned = "DESIGNER_NOT_ASSIGNED";
    public const string CatalogProductNotEligible = "CATALOG_PRODUCT_NOT_ELIGIBLE";
    public const string CatalogVersionNotEligible = "CATALOG_VERSION_NOT_ELIGIBLE";
}
