namespace FurniSpace.Application.Constants.Accounts;

internal static class AccountServiceConstants
{
    internal const string AccountIndexName = "accounts";
    internal const string AccountItemCachePrefix = "furnispace:accounts:item:";
    internal const string AccountListCachePrefix = "furnispace:accounts:list:";
    internal const string AccountNotFoundCode = "ACCOUNT_NOT_FOUND";
    internal const string AccountNotFoundMessage = "Account not found.";
    internal const string AccountDetailRetrievedMessage = "Account detail retrieved successfully.";
    internal const string ProfileUpdatedMessage = "Profile updated successfully.";
    internal const string AvailableDesignersRetrievedMessage = "Available designers retrieved successfully.";
    internal const string DesignerWorkloadRetrievedMessage = "Designer workload retrieved successfully.";
    internal const string DesignerWorkloadSummaryRetrievedMessage = "Designer workload summary retrieved successfully.";
    internal const string DesignerAssignedProjectsRetrievedMessage = "Designer assigned projects retrieved successfully.";
    internal const string SalesWorkloadRetrievedMessage = "Sales workload retrieved successfully.";
    internal const string SalesWorkloadSummaryRetrievedMessage = "Sales workload summary retrieved successfully.";
    internal const string SalesAssignedProjectsRetrievedMessage = "Sales assigned projects retrieved successfully.";
    internal const string UnassignedIntakeProjectsRetrievedMessage = "Unassigned intake projects retrieved successfully.";
    internal const int MaxActiveDesignerProjects = 2;
    internal const int MaxActiveSalesProjects = 5;
    internal const string PageMustBeGreaterThanZero = "Page must be greater than zero.";
    internal const string PageSizeMustBeBetween1And100 = "Page size must be between 1 and 100.";
    internal const string SortDesignActiveCountDesc = "DesignActiveCountDesc";
    internal const string SortAvailableSlotDesc = "AvailableSlotDesc";
    internal const string SortFuturePressureScoreDesc = "FuturePressureScoreDesc";
    internal const string SortSalesActiveCountDesc = "SalesActiveCountDesc";
    internal const string SortAvailableSlotAsc = "AvailableSlotAsc";

    internal static readonly TimeSpan AccountItemCacheTtl = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan AccountListCacheTtl = TimeSpan.FromMinutes(5);
}
