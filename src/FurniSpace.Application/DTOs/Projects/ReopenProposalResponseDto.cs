using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ReopenProposalResponseDto
{
    public Guid ProjectId { get; set; }
    public ProjectStatus? OldStatus { get; set; }
    public ProjectStatus? NewStatus { get; set; }
    public Guid? OrderId { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public Guid? QuotationId { get; set; }
    public QuotationStatus? QuotationStatus { get; set; }
    public Guid? SelectedProposalId { get; set; }
    public ProposalStatus? SelectedProposalStatus { get; set; }
    public int RestoredProposalCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
