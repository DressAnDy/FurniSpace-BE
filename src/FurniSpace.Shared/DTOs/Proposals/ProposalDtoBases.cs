#nullable enable

namespace FurniSpace.Shared.DTOs.Proposals;

public abstract class ProposalBaseDto<TStatus>
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentProposalId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? VersionNo { get; set; }
    public TStatus Status { get; set; } = default!;
    public DateTime? PublishedAt { get; set; }
    public DateTime? SelectedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public abstract class ProposalSceneBaseDto<TSceneType>
{
    public Guid SceneId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string? SceneName { get; set; }
    public TSceneType SceneType { get; set; } = default!;
    public string? MongoSceneId { get; set; }
    public Guid? PreviewFileId { get; set; }
    public string? PreviewFileUrl { get; set; }
    public int? VersionNo { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
