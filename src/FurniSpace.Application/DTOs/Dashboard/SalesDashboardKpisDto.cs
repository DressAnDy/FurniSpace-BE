namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class SalesDashboardKpisDto
{
    public int NewRequests { get; set; }

    public int WaitingCustomer { get; set; }

    public int PaymentFollowUp { get; set; }

    public int OverdueTasks { get; set; }

    public int ActiveProjects { get; set; }
}
