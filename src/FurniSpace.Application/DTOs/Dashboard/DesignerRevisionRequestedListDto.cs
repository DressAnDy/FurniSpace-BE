namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DesignerRevisionRequestedListResponseDto
{
    public IReadOnlyList<DesignerRevisionRequestedItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class DesignerRevisionRequestedItemDto
{
    public Guid ProposalId { get; set; }

    public string ProposalName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? RevisionNote { get; set; }

    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public Guid? AssignedDesignerId { get; set; }

    public string? AssignedDesignerName { get; set; }

    /// <summary>Timestamp when revision was requested; sourced from proposal <c>updatedAt</c>.</summary>
    public DateTime? RevisionRequestedAt { get; set; }
}
