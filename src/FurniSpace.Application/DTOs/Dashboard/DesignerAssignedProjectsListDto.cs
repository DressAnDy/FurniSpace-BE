namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DesignerAssignedProjectsListResponseDto
{
    public IReadOnlyList<DesignerAssignedProjectItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class DesignerAssignedProjectItemDto
{
    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public DateTime? DesignerAssignedAt { get; set; }

    public bool HasCustomerCustomizationRequest { get; set; }

    public int OpenCustomizationRequestCount { get; set; }

    public string? LatestCustomizationStatus { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
