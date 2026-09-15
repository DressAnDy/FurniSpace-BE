namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DesignerProposalConsultingListResponseDto
{
    public IReadOnlyList<DesignerProposalConsultingItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class DesignerProposalConsultingItemDto
{
    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public Guid? AssignedDesignerId { get; set; }

    public string? AssignedDesignerName { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? DesignerAssignedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
}
