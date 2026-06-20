using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProposalSceneVariant
{
    public Guid VariantId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid SceneId { get; set; }
    public Guid CreatedBy { get; set; }
    public ProposalSceneVariantType? VariantType { get; set; }
    public ProposalSceneVariantStatus? Status { get; set; }
    public string MongoVariantSceneId { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? AppliedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
