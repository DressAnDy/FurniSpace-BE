namespace FurniSpace.Application.DTOs.Dashboard;

/// <summary>
/// Sales dashboard KPI cards. Stock counts ignore <c>dateRange</c> and <c>search</c>.
/// </summary>
public sealed class SalesDashboardKpisDto
{
    /// <summary>
    /// Projects currently assigned to the scoped sales user (<c>assignedSalesId</c> set).
    /// Includes <c>COMPLETED</c> and <c>REJECTED</c>. Does not count unassigned <c>SUBMITTED</c> requests.
    /// </summary>
    public int AcceptedProjects { get; set; }

    /// <summary>
    /// Orders whose stage-3 remaining payment has been incurred and is not collected.
    /// Does not include start fee, deposit, in-production, or pre-confirmation delivery orders.
    /// </summary>
    public int UnpaidRemaining { get; set; }

    /// <summary>
    /// Projects in scope whose <c>targetCompletionDate</c> is before today (UTC).
    /// No project status is excluded. Ignores <c>dateRange</c> and <c>search</c>.
    /// </summary>
    public int OverdueTasks { get; set; }

    /// <summary>Compatibility. Unassigned <c>SUBMITTED</c> requests in the filtered queue. Not a stock card.</summary>
    public int NewRequests { get; set; }

    /// <summary>Compatibility. Not used by the sales KPI cards.</summary>
    public int WaitingCustomer { get; set; }

    /// <summary>
    /// Compatibility. Old follow-up count (deposit pending, or final payment pending with remaining amount).
    /// Do not map this to <see cref="UnpaidRemaining"/>.
    /// </summary>
    public int PaymentFollowUp { get; set; }

    /// <summary>
    /// Compatibility. Old non-terminal project count. Do not map this to <see cref="AcceptedProjects"/>.
    /// </summary>
    public int ActiveProjects { get; set; }
}
