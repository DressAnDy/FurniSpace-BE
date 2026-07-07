using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Quotations;

public sealed class SelectedProposalForQuotationReadModel
{
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
}
