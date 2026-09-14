namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class SalesOverdueTasksListResponseDto
{
    public IReadOnlyList<SalesOverdueTaskItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class SalesOverdueTaskItemDto
{
    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public Guid? AssignedSalesId { get; set; }

    public string? AssignedSalesName { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateOnly TargetCompletionDate { get; set; }

    public int OverdueDays { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
