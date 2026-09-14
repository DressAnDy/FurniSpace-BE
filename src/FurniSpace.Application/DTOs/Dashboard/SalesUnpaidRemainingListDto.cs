namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class SalesUnpaidRemainingListResponseDto
{
    public IReadOnlyList<SalesUnpaidRemainingItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class SalesUnpaidRemainingItemDto
{
    public Guid OrderId { get; set; }

    public string? OrderCode { get; set; }

    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public Guid? AssignedSalesId { get; set; }

    public string? AssignedSalesName { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal RemainingAmount { get; set; }

    public string? Currency { get; set; }

    public Guid? PaymentId { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime UpdatedAt { get; set; }
}
