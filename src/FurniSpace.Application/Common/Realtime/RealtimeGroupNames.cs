namespace FurniSpace.Application.Common.Realtime;

public static class RealtimeGroupNames
{
    public const string HubPath = "/hubs/notifications";

    public static string User(Guid accountId) => $"user:{accountId:D}";

    public static string User(string accountId) => $"user:{accountId}";

    public static string Role(string roleName) => $"role:{roleName.Trim().ToUpperInvariant()}";
}
