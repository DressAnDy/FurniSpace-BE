using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalDetailReadModel
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentProposalId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? VersionNo { get; set; }
    public ProposalStatus? Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? SelectedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public IReadOnlyList<ProposalSceneReadModel> Scenes { get; set; } = [];
    public IReadOnlyList<ProposalItemReadModel> Items { get; set; } = [];
}
