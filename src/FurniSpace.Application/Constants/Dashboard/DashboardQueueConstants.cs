namespace FurniSpace.Application.Constants.Dashboard;

internal static class DashboardQueueConstants
{
    internal const string ScopeMine = "mine";
    internal const string ScopeTeam = "team";
    internal const string ScopeAll = "all";

    internal const string DateRangeToday = "today";
    internal const string DateRangeThisWeek = "thisWeek";
    internal const string DateRangeThisMonth = "thisMonth";

    internal const string PriorityHigh = "HIGH";
    internal const string PriorityMedium = "MEDIUM";
    internal const string PriorityLow = "LOW";

    internal const string DueBucketOverdue = "OVERDUE";
    internal const string DueBucketToday = "TODAY";
    internal const string DueBucketThisWeek = "THIS_WEEK";
    internal const string DueBucketLater = "LATER";

    internal const string GroupIntake = "Intake";
    internal const string GroupDesign = "Design";
    internal const string GroupProposalAndQuotation = "Proposal and Quotation";
    internal const string GroupOrderAndPayment = "Order and Payment";
    internal const string GroupDelivery = "Delivery";
    internal const string GroupProduction = "Production";

    internal const int DefaultPage = 1;
    internal const int DefaultLimit = 20;
    internal const int MaxLimit = 100;
}
