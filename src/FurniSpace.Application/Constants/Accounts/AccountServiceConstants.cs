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
    internal const int MaxActiveDesignerProjects = 2;

    internal static readonly TimeSpan AccountItemCacheTtl = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan AccountListCacheTtl = TimeSpan.FromMinutes(5);
}
