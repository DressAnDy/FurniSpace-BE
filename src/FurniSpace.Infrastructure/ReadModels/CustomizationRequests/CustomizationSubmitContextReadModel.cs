using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class CustomizationSubmitContextReadModel
{
    public Guid ProposalItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public string ProposalName { get; set; } = string.Empty;
}
