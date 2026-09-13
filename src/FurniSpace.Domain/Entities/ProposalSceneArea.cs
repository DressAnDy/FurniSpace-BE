using System;

namespace FurniSpace.Domain.Entities;

public class ProposalSceneArea
{
    public Guid ProposalSceneAreaId { get; set; }
    public Guid SceneId { get; set; }
    public Guid ProjectAreaId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ProposalScene? Scene { get; set; }
    public ProjectArea? ProjectArea { get; set; }
}
