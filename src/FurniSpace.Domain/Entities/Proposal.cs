using System;

namespace FurniSpace.Domain.Entities;

public class Proposal
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentProposalId { get; set; }
    public string ProposalName { get; set; } = null!;
    public string? Description { get; set; }
    public string? DesignConcept { get; set; }
    public int? VersionNo { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Status { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? SelectedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


