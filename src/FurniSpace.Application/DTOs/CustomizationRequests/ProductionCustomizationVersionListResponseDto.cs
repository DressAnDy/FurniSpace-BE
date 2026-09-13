using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionCustomizationProjectSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}

public sealed class ProductionCustomizationProposalSummaryDto
{
    public Guid ProposalId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public ProposalStatus? Status { get; set; }
}

public sealed class ProductionCustomizationVersionListResponseDto
{
    public IReadOnlyList<ProductionCustomizationVersionQueueItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class ProductionCustomizationVersionQueueItemDto
{
    public CustomizationRequestVersionDto Version { get; set; } = new();
    public CustomizationRequestDto Request { get; set; } = new();
    public ProductionCustomizationProjectSummaryDto Project { get; set; } = new();
    public ProductionCustomizationProposalSummaryDto Proposal { get; set; } = new();
    public ApprovedProductVersionSummaryDto SourceProductVersion { get; set; } = new();
}

public sealed class ProductionCustomizationVersionDetailDto
{
    public CustomizationRequestVersionDto Version { get; set; } = new();
    public CustomizationRequestDto Request { get; set; } = new();
    public ProductionCustomizationProjectSummaryDto Project { get; set; } = new();
    public ProductionCustomizationProposalSummaryDto Proposal { get; set; } = new();
    public ApprovedProductVersionSummaryDto SourceProductVersion { get; set; } = new();
}
