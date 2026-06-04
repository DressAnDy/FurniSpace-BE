using System;

namespace FurniSpace.Domain.Entities;

public class ProposalScene
{
    public Guid SceneId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string? SceneName { get; set; }
    public string? SceneType { get; set; }
    public string? MongoSceneId { get; set; }
    public Guid? PreviewFileId { get; set; }
    public int? VersionNo { get; set; }
    public bool? IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


